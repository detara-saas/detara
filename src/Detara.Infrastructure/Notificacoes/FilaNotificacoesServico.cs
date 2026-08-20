using Detara.Application.Abstracoes;
using Detara.Application.Notificacoes;
using Detara.Domain.Notificacoes;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;

namespace Detara.Infrastructure.Notificacoes;

public sealed class FilaNotificacoesOptions
{
    public const string Secao = "Notificacoes:Fila";
    public int TamanhoLote { get; init; } = 20;
    public int IntervaloSegundos { get; init; } = 15;
    public int MaximoTentativas { get; init; } = 4;
    public int ProcessamentoExpiraMinutos { get; init; } = 10;
}

internal sealed class FilaNotificacoesServico(IServiceScopeFactory scopeFactory,
    IOptions<FilaNotificacoesOptions> options, ILogger<FilaNotificacoesServico> logger) : IFilaNotificacoesServico
{
    public async Task<int> ProcessarLoteAsync(CancellationToken ct)
    {
        var config = options.Value;
        var lote = Math.Clamp(config.TamanhoLote, 10, 50);
        List<(Guid Id, Guid EmpresaId)> candidatos;
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            var dbOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
            await using var sistema = new DetaraDbContext(dbOptions, ContextoFila.Anonimo);
            var agora = DateTime.UtcNow;
            var expirou = agora.AddMinutes(-Math.Clamp(config.ProcessamentoExpiraMinutos, 2, 60));
            candidatos = await sistema.NotificacoesEmail.IgnoreQueryFilters().AsNoTracking()
                .Where(x => (x.Status == StatusNotificacaoEmail.Pendente && x.ProximaTentativaEmUtc <= agora) ||
                    (x.Status == StatusNotificacaoEmail.Processando && x.ProcessamentoIniciadoEmUtc <= expirou))
                .OrderBy(x => x.ProximaTentativaEmUtc).ThenBy(x => x.CriadoEmUtc)
                .Take(lote).Select(x => new ValueTuple<Guid, Guid>(x.Id, x.EmpresaId)).ToListAsync(ct);
        }

        var processadas = 0;
        foreach (var candidato in candidatos)
        {
            if (await ProcessarAsync(candidato.Id, candidato.EmpresaId, config, ct)) processadas++;
        }
        return processadas;
    }

    private async Task<bool> ProcessarAsync(Guid id, Guid empresaId, FilaNotificacoesOptions config, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var dbOptions = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
        var provedor = scope.ServiceProvider.GetRequiredService<IProvedorEmail>();
        await using var db = new DetaraDbContext(dbOptions, new ContextoFila(empresaId));
        var item = await db.NotificacoesEmail.Include(x => x.Tentativas).SingleOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return false;
        try
        {
            var agora = DateTime.UtcNow;
            if (item.Status == StatusNotificacaoEmail.Processando &&
                item.ProcessamentoIniciadoEmUtc <= agora.AddMinutes(-Math.Clamp(config.ProcessamentoExpiraMinutos, 2, 60)))
            {
                item.RecuperarProcessamentoInterrompido(agora);
                await db.SaveChangesAsync(ct);
            }
            if (item.Status != StatusNotificacaoEmail.Pendente || item.ProximaTentativaEmUtc > agora) return false;
            item.MarcarProcessando(agora);
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException) { return false; }

        var tipoTentativa = item.TipoProximaTentativa;
        var solicitadoPor = item.ProximaTentativaSolicitadaPorUsuarioId;
        var resultado = await provedor.EnviarAsync(new(item.DestinatarioEmailSnapshot!, item.AssuntoSnapshot,
            item.CorpoHtmlSnapshot, item.ResponderParaSnapshot, $"notificacao-email/{item.Id:N}"), ct);
        var finalizadoEm = DateTime.UtcNow;
        TentativaNotificacaoEmail tentativa;
        if (resultado.Sucesso)
            tentativa = item.RegistrarSucesso(resultado.MensagemId ?? "aceita-sem-id", finalizadoEm, tipoTentativa, solicitadoPor);
        else
        {
            var proxima = CalcularProxima(finalizadoEm, item.QuantidadeTentativas + 1);
            tentativa = item.RegistrarFalha(resultado.ErroSeguro ?? "Falha no provedor de e-mail.", resultado.FalhaTemporaria,
                Math.Clamp(config.MaximoTentativas, 1, 10), finalizadoEm, proxima, tipoTentativa, solicitadoPor);
        }
        db.TentativasNotificacaoEmail.Add(tentativa);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException ex)
        {
            logger.LogWarning(ex, "Concorrência ao concluir notificação {NotificacaoId} da empresa {EmpresaId}.", id, empresaId);
            return false;
        }
        return true;
    }

    private static DateTime CalcularProxima(DateTime agora, int numeroTentativa) => numeroTentativa switch
    {
        <= 1 => agora.AddMinutes(1),
        2 => agora.AddMinutes(5),
        _ => agora.AddMinutes(30)
    };

    private sealed class ContextoFila(Guid empresaId) : IUsuarioContexto
    {
        public static ContextoFila Anonimo { get; } = new(Guid.Empty);
        public Guid UsuarioId => EmpresaId == Guid.Empty ? Guid.Empty : Guid.Parse("00000000-0000-0000-0000-000000000001");
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => EmpresaId != Guid.Empty;
    }
}

public sealed class NotificacoesWorker(IServiceScopeFactory scopeFactory, IOptions<FilaNotificacoesOptions> options,
    ILogger<NotificacoesWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromSeconds(Math.Clamp(options.Value.IntervaloSegundos, 5, 300));
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IFilaNotificacoesServico>().ProcessarLoteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Falha ao processar a fila de notificações por e-mail."); }
            await Task.Delay(intervalo, stoppingToken);
        }
    }
}
