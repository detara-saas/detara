using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Detara.Infrastructure.Financeiro;

internal static class CategoriasDespesaIniciais
{
    internal static readonly string[] Nomes = ["Aluguel", "Impostos", "Água", "Energia", "Internet e telefonia",
        "Produtos e insumos", "Equipamentos", "Manutenção", "Marketing", "Serviços de terceiros", "Pessoal", "Software e assinaturas", "Outros"];
    // Chamado na fundação do tenant e no backfill. Nunca recria categorias renomeadas/inativadas.
    internal static async Task PrepararAsync(DetaraDbContext db, Guid empresaId, CancellationToken ct)
    {
        if (db.CategoriasDespesa.Local.Any() || await db.CategoriasDespesa.AnyAsync(ct)) return;
        db.CategoriasDespesa.AddRange(Nomes.Select(nome => new CategoriaDespesa(empresaId, nome)));
    }
}

public sealed class MaterializadorDespesas(IServiceScopeFactory scopes, TimeProvider relogio,
    IConversorFusoHorario conversor, ILogger<MaterializadorDespesas> logger)
{
    public async Task<int> ExecutarAsync(CancellationToken ct = default)
    {
        using var scope = scopes.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
        await using var consulta = new DetaraDbContext(options, new ContextoConsulta());
        // Projeção administrativa mínima: identifica tenants ativos e seu fuso para o processamento interno.
        var ultimoId = Guid.Empty;
        var geradas = 0;
        while (!ct.IsCancellationRequested)
        {
            var empresas = await consulta.Empresas.AsNoTracking().Where(x => x.EhAtivo && x.Id.CompareTo(ultimoId) > 0)
                .OrderBy(x => x.Id).Select(x => new { x.Id, x.FusoHorario }).Take(100).ToArrayAsync(ct);
            if (empresas.Length == 0) break;
            foreach (var empresa in empresas)
            {
                try
                {
                    var hoje = DateOnly.FromDateTime(conversor.ParaLocal(relogio.GetUtcNow().UtcDateTime, empresa.FusoHorario));
                    await using var db = DetaraDbContext.ParaProcessamentoFinanceiro(options, empresa.Id);
                    await CategoriasDespesaIniciais.PrepararAsync(db, empresa.Id, ct);
                    try { await db.SaveChangesAsync(ct); }
                    catch (DbUpdateException ex) when (Duplicidade(ex))
                    {
                        db.ChangeTracker.Clear();
                        if (!await db.CategoriasDespesa.AnyAsync(ct)) throw;
                    }
                    var ultimoRegra = Guid.Empty;
                    while (!ct.IsCancellationRequested)
                    {
                        var ids = await db.DespesasRecorrentes.AsNoTracking()
                            .Where(x => x.Id.CompareTo(ultimoRegra) > 0 && x.EhAtivo && x.ProximaCompetencia <= hoje &&
                                (x.CompetenciaFinal == null || x.ProximaCompetencia <= x.CompetenciaFinal))
                            .OrderBy(x => x.Id).Select(x => x.Id).Take(100).ToArrayAsync(ct);
                        if (ids.Length == 0) break;
                        foreach (var id in ids)
                        {
                            try { geradas += await MaterializarAsync(options, empresa.Id, id, hoje, ct); }
                            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                            catch (Exception ex) { logger.LogError(ex, "Falha ao materializar recorrência {RecorrenciaId} da empresa {EmpresaId}.", id, empresa.Id); }
                        }
                        ultimoRegra = ids[^1];
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
                catch (Exception ex) { logger.LogError(ex, "Falha no ciclo de despesas da empresa {EmpresaId}.", empresa.Id); }
            }
            ultimoId = empresas[^1].Id;
        }
        if (geradas > 0) logger.LogInformation("Materialização mensal concluiu {Quantidade} contas a pagar.", geradas);
        else logger.LogDebug("Ciclo de despesas sem novas contas.");
        return geradas;
    }

    internal static async Task<int> MaterializarAsync(DbContextOptions<DetaraDbContext> options,
        Guid empresaId, Guid id, DateOnly hoje, CancellationToken ct)
    {
        var geradas = 0;
        for (var tentativa = 0; tentativa < 3; tentativa++)
        {
            await using var db = DetaraDbContext.ParaProcessamentoFinanceiro(options, empresaId);
            try
            {
                var regra = await db.DespesasRecorrentes.SingleOrDefaultAsync(x => x.Id == id, ct);
                if (regra == null || !regra.PodeMaterializar(hoje)) return geradas;
                var categoria = await db.CategoriasDespesa.SingleAsync(x => x.Id == regra.CategoriaDespesaId, ct);
                // Uma transação por ocorrência: snapshot e cursor avançam juntos; Versao protege também edição/inativação.
                while (regra.PodeMaterializar(hoje))
                {
                    var existe = await db.ContasPagar.AnyAsync(x => x.DespesaRecorrenteId == id && x.Competencia == regra.ProximaCompetencia, ct);
                    if (existe) regra.AvancarCompetencia();
                    else db.ContasPagar.Add(regra.Materializar(categoria, hoje));
                    await db.SaveChangesAsync(ct);
                    if (!existe) geradas++;
                }
                return geradas;
            }
            catch (DbUpdateConcurrencyException) when (tentativa < 2) { }
            catch (DbUpdateException ex) when (tentativa < 2 && Duplicidade(ex)) { }
        }
        return geradas;
    }

    private static bool Duplicidade(DbUpdateException ex) => ex.InnerException is SqlException { Number: 2601 or 2627 };
    private sealed class ContextoConsulta : IUsuarioContexto
    { public Guid UsuarioId => Guid.Empty; public Guid EmpresaId => Guid.Empty; public bool EstaAutenticado => false; }
}

internal sealed class DespesasWorker(MaterializadorDespesas materializador, ILogger<DespesasWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        do
        {
            try { await materializador.ExecutarAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Falha ao iniciar o processamento mensal de despesas."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
