using Detara.Application.Abstracoes;
using Detara.Application.Financeiro;
using Detara.Domain.Entidades;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Financeiro;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Detara.Application.Agenda;

namespace Detara.IntegrationTests.Financeiro;

public sealed class DespesasPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private readonly Guid _a = Guid.NewGuid(), _b = Guid.NewGuid();
    private static readonly DateOnly Hoje = new(2026, 9, 7);
    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        await using var db = Contexto(Guid.Empty); await db.Database.EnsureCreatedAsync();
    }
    public async Task DisposeAsync() => await _connection.DisposeAsync();
    private DetaraDbContext Contexto(Guid id) => new(_options, new Usuario(id));
    private async Task<(Guid Categoria, Guid Regra)> PrepararAsync(Guid id)
    {
        await using var db = Contexto(id);
        var categoria = new CategoriaDespesa(id, "Aluguel");
        var regra = new DespesaRecorrente(id, "Aluguel", categoria, 4500, 31, new(2026, 9, 1), null, Hoje);
        db.AddRange(categoria, regra); await db.SaveChangesAsync(); return (categoria.Id, regra.Id);
    }
    [Fact]
    public async Task CicloWorker_ProcessaMultiplosTenants_EBackfillSemUsuario()
    {
        await PrepararAsync(_a); await PrepararAsync(_b);
        await using (var db = Contexto(Guid.Empty))
        {
            foreach (var id in new[] { _a, _b })
            {
                var empresa = new Empresa("Teste", "Teste", id == _a ? "11111111000111" : "22222222000122", id.ToString("N"));
                typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(empresa, id); db.Add(empresa);
            }
            await db.SaveChangesAsync();
        }
        var services = new ServiceCollection(); services.AddSingleton(_options);
        await using var provider = services.BuildServiceProvider();
        var worker = new MaterializadorDespesas(provider.GetRequiredService<IServiceScopeFactory>(), new Relogio(),
            new ConversorFusoHorario(), NullLogger<MaterializadorDespesas>.Instance);
        Assert.Equal(2, await worker.ExecutarAsync()); Assert.Equal(0, await worker.ExecutarAsync());
        await using var a = Contexto(_a); await using var b = Contexto(_b);
        Assert.Single(await a.ContasPagar.ToArrayAsync()); Assert.Single(await b.ContasPagar.ToArrayAsync());
    }
    private sealed class Relogio : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero); }
    [Fact]
    public async Task Materializador_Catchup_Idempotente_TenantExplicito()
    {
        var a = await PrepararAsync(_a); var b = await PrepararAsync(_b);
        Assert.Equal(4, await MaterializadorDespesas.MaterializarAsync(_options, _a, a.Regra, new(2026, 12, 7), default));
        Assert.Equal(0, await MaterializadorDespesas.MaterializarAsync(_options, _a, a.Regra, new(2026, 12, 7), default));
        Assert.Equal(0, await MaterializadorDespesas.MaterializarAsync(_options, _a, b.Regra, Hoje, default));
        await using var db = Contexto(_a); Assert.Equal(4, await db.ContasPagar.CountAsync());
        Assert.Equal(new DateOnly(2027, 1, 1), (await db.DespesasRecorrentes.SingleAsync()).ProximaCompetencia);
        await using var outro = Contexto(_b); Assert.Empty(await outro.ContasPagar.ToArrayAsync());
        await using var anonimo = Contexto(Guid.Empty); Assert.Empty(await anonimo.ContasPagar.ToArrayAsync());
    }
    [Fact]
    public async Task UniqueCompetencia_EConcorrencia_NaoPermitemDuasInstanciasGeraremMes()
    {
        var ids = await PrepararAsync(_a);
        await using var d1 = Contexto(_a); await using var d2 = Contexto(_a);
        var r1 = await d1.DespesasRecorrentes.SingleAsync(); var r2 = await d2.DespesasRecorrentes.SingleAsync();
        var c1 = await d1.CategoriasDespesa.SingleAsync(); var c2 = await d2.CategoriasDespesa.SingleAsync();
        var primeiro = r1.Materializar(c1, Hoje); var segundo = r2.Materializar(c2, Hoje);
        d1.Add(primeiro); d2.Add(segundo); await d1.SaveChangesAsync();
        await Assert.ThrowsAnyAsync<DbUpdateException>(() => d2.SaveChangesAsync());
        await using var verificar = Contexto(_a); Assert.Single(await verificar.ContasPagar.ToArrayAsync());
        // Mesmo sem atualizar o cursor, a restrição única protege a ocorrência.
        verificar.Add(segundo); await Assert.ThrowsAsync<DbUpdateException>(() => verificar.SaveChangesAsync());
    }
    [Fact]
    public async Task Concorrencia_InativacaoVenceMaterializacao_ESnapshotNaoEhInserido()
    {
        await PrepararAsync(_a);
        await using var worker = DetaraDbContext.ParaProcessamentoFinanceiro(_options, _a);
        var r = await worker.DespesasRecorrentes.SingleAsync();
        worker.Add(r.Materializar(await worker.CategoriasDespesa.SingleAsync(), Hoje));
        await using (var usuario = Contexto(_a)) { (await usuario.DespesasRecorrentes.SingleAsync()).DefinirAtividade(false, Hoje); await usuario.SaveChangesAsync(); }
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => worker.SaveChangesAsync());
        await using var verificar = Contexto(_a); Assert.Empty(await verificar.ContasPagar.ToArrayAsync());
    }
    [Fact]
    public async Task Pagamento_EEstorno_PersistemHistorico_ERejeitamEdicaoConcorrente()
    {
        var ids = await PrepararAsync(_a); await MaterializadorDespesas.MaterializarAsync(_options, _a, ids.Regra, Hoje, default);
        await using var db = Contexto(_a); await using var stale = Contexto(_a);
        var conta = await db.ContasPagar.Include(x => x.Pagamentos).SingleAsync();
        var antiga = await stale.ContasPagar.SingleAsync();
        var p = conta.RegistrarPagamento(Hoje, 4490, _a, DateTime.UtcNow); db.Add(p); await db.SaveChangesAsync();
        antiga.Cancelar(_a, DateTime.UtcNow); await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => stale.SaveChangesAsync());
        conta.EstornarPagamento(_a, "Erro de lançamento", DateTime.UtcNow); await db.SaveChangesAsync();
        db.Add(conta.RegistrarPagamento(Hoje, 4500, _a, DateTime.UtcNow)); await db.SaveChangesAsync();
        db.ChangeTracker.Clear(); var persisted = await db.ContasPagar.Include(x => x.Pagamentos).SingleAsync();
        Assert.Equal(2, persisted.Pagamentos.Count); Assert.Equal(4500, persisted.ValorPago);
        Assert.Single(persisted.Pagamentos, x => x.Status == StatusPagamento.Confirmado);
    }
    [Fact]
    public async Task Resumo_Filtros_Competencia_PagoReal_CanceladasFora()
    {
        await PrepararAsync(_a); await PrepararAsync(_b);
        await using var db = Contexto(_a); var c = await db.CategoriasDespesa.SingleAsync();
        ContaPagar Nova(decimal valor, DateOnly vencimento) => new(_a, "Insumos", c, valor, new(2026, 9, 1), vencimento, "Fornecedor");
        var vencida = Nova(100, Hoje.AddDays(-1)); var pendente = Nova(200, Hoje); var paga = Nova(300, Hoje.AddDays(-1)); var cancelada = Nova(400, Hoje);
        paga.RegistrarPagamento(Hoje, 290, _a, DateTime.UtcNow); cancelada.Cancelar(_a, DateTime.UtcNow);
        db.AddRange(vencida, pendente, paga, cancelada); await db.SaveChangesAsync();
        var repo = new DespesasRepositorio(db);
        var r = await repo.ListarAsync(new(Status: FiltroStatusDespesa.Vencido, Pesquisa: "Fornecedor", CategoriaId: c.Id, Origem: OrigemContaPagar.Avulsa), Hoje, default);
        Assert.Single(r.Contas.Itens); Assert.Equal(new ResumoDespesasResultado(600, 290, 200, 100), r.Resumo);
        Assert.Equal(0, (await repo.ListarAsync(new(Competencia: new(2026, 10, 1)), Hoje, default)).Resumo.Total);
        await using var outro = Contexto(_b); Assert.Empty((await new DespesasRepositorio(outro).ListarAsync(new(), Hoje, default)).Contas.Itens);
    }
    [Fact]
    public async Task Categorias_Defaults_Idempotentes_RenomearInativar_NaoRecria()
    {
        await using var db = Contexto(_a);
        await CategoriasDespesaIniciais.PrepararAsync(db, _a, default); await db.SaveChangesAsync();
        var categoria = await db.CategoriasDespesa.FirstAsync(); categoria.Renomear("Personalizada"); categoria.DefinirAtividade(false); await db.SaveChangesAsync();
        await CategoriasDespesaIniciais.PrepararAsync(db, _a, default); await db.SaveChangesAsync();
        Assert.Equal(CategoriasDespesaIniciais.Nomes.Length, await db.CategoriasDespesa.CountAsync());
        db.Add(new CategoriaDespesa(_a, " personalizada ")); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task FKsCompostas_ImpedemCategoriaDeOutroTenant_EDeleteComHistorico()
    {
        var a = await PrepararAsync(_a); var b = await PrepararAsync(_b);
        await using var db = Contexto(_a); var cat = await db.CategoriasDespesa.SingleAsync();
        var conta = new ContaPagar(_a, "Teste", cat, 100, new(2026, 9, 1), Hoje);
        typeof(ContaPagar).GetProperty(nameof(ContaPagar.CategoriaDespesaId))!.SetValue(conta, b.Categoria);
        db.Add(conta); await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear(); db.Remove(await db.CategoriasDespesa.SingleAsync());
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
    [Fact]
    public async Task FronteiraWorker_NaoPermiteOutroTenant_PagamentosOuEntidadesGlobais()
    {
        await PrepararAsync(_a);
        await using var db = DetaraDbContext.ParaProcessamentoFinanceiro(_options, _a);
        db.Add(new CategoriaDespesa(_b, "Invasão")); await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear(); db.Add(new Empresa("Outra", "Outra", "11111111000111", "outra"));
        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear(); db.Remove(await db.CategoriasDespesa.SingleAsync());
        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => db.SaveChangesAsync());
        await using var anonimo = Contexto(Guid.Empty); anonimo.Add(new CategoriaDespesa(_a, "X"));
        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => anonimo.SaveChangesAsync());
    }
    private sealed class Usuario(Guid empresa) : IUsuarioContexto
    { public Guid EmpresaId => empresa; public Guid UsuarioId => empresa; public bool EstaAutenticado => empresa != Guid.Empty; }
}
