using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Application.Financeiro;
using Detara.Domain.Atendimento;
using Detara.Domain.Entidades;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Financeiro;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Atendimento;

public sealed class AtendimentoRapidoPersistenciaTests : IAsyncLifetime
{
    private readonly Guid _empresaId = Guid.NewGuid();
    private readonly Guid _usuarioId = Guid.NewGuid();
    private DbContextOptions<DetaraDbContext> _options = null!;
    private SqliteConnection? _sqlite;

    public async Task InitializeAsync()
    {
        var sql = Environment.GetEnvironmentVariable("DETARA_QUICK_ENTRY_TEST_SQL");
        if (string.IsNullOrWhiteSpace(sql))
        {
            _sqlite = new("Data Source=:memory:");
            await _sqlite.OpenAsync();
            _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_sqlite).Options;
        }
        else
        {
            var connection = new SqlConnectionStringBuilder(sql)
            {
                InitialCatalog = $"DetaraQuickEntryQa_{Guid.NewGuid():N}"
            };
            _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlServer(connection.ConnectionString).Options;
        }

        await using var db = new DetaraDbContext(_options, new UsuarioTeste(Guid.Empty, false));
        if (_sqlite is null) await db.Database.MigrateAsync();
        else await db.Database.EnsureCreatedAsync();
        var empresa = new Empresa("QA Atendimento Rápido", "QA Atendimento Rápido", "12345678000190", $"qa-{_empresaId:N}");
        typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(empresa, _empresaId);
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        if (_sqlite is not null) await _sqlite.DisposeAsync();
    }

    [Fact]
    public async Task SolicitacoesDisputandoMesmaChave_PersistemSomenteUmaOsContaEPagamento()
    {
        var solicitacaoId = Guid.NewGuid();
        await using var primeiro = Contexto();
        await using var segundo = Contexto();
        var a = Ordem(solicitacaoId);
        var b = Ordem(solicitacaoId);
        await PrepararAsync(primeiro, a);
        await PrepararAsync(segundo, b);
        await primeiro.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateException>(() => segundo.SaveChangesAsync());

        await using var verificar = Contexto();
        Assert.Single(await verificar.OrdensServico.ToArrayAsync());
        var conta = Assert.Single(await verificar.ContasReceber.Include(x => x.Pagamentos).ToArrayAsync());
        Assert.Equal(a.Id, conta.OrdemServicoId);
        Assert.Single(conta.Pagamentos);
        Assert.Equal(150, conta.ValorRecebido);
        Assert.Equal(1, await verificar.Pagamentos.CountAsync());
    }

    [Fact]
    public async Task FalhaNoPagamento_NaoPersisteParteDoAtendimento()
    {
        await using var db = Contexto();
        var ordem = Ordem(Guid.NewGuid());
        db.OrdensServico.Add(ordem);
        var integracao = Integracao(db);
        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            integracao.PrepararRecebimentoIntegralAsync(Evento(ordem), FormaPagamento.Pix,
                151, null, null, _usuarioId, default));

        await using var verificar = Contexto();
        Assert.Equal(0, await verificar.OrdensServico.CountAsync());
        Assert.Equal(0, await verificar.ContasReceber.CountAsync());
        Assert.Equal(0, await verificar.Pagamentos.CountAsync());
    }

    [Fact]
    public async Task PagamentoIntegral_RespeitaTaxaEstornoECompetenciaNoFusoDaEmpresa()
    {
        await using var db = Contexto();
        var ordem = Ordem(Guid.NewGuid());
        await PrepararAsync(db, ordem, FormaPagamento.CartaoCredito, 3, 2);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var conta = await db.ContasReceber.Include(x => x.Pagamentos).SingleAsync();
        var pagamento = Assert.Single(conta.Pagamentos);
        Assert.Equal(147, pagamento.ValorLiquido);
        Assert.Equal(2, pagamento.NumeroParcelas);
        Assert.Equal(StatusContaReceber.Pago, conta.Status);
        Assert.Equal(DateOnly.FromDateTime(new ConversorFusoHorario().ParaLocal(
            ordem.ConcluidaEmUtc!.Value, "America/Sao_Paulo")), conta.DataCompetencia);
        conta.EstornarPagamento(pagamento.Id, _usuarioId, "Lançamento incorreto", DateTime.UtcNow);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        var atual = await db.ContasReceber.Include(x => x.Pagamentos).SingleAsync();
        Assert.Equal(StatusContaReceber.EmAberto, atual.Status);
        Assert.Equal(StatusPagamento.Estornado, Assert.Single(atual.Pagamentos).Status);
        Assert.Equal(StatusOrdemServico.Concluida, (await db.OrdensServico.SingleAsync()).Status);
    }

    private async Task PrepararAsync(DetaraDbContext db, OrdemServico ordem,
        FormaPagamento forma = FormaPagamento.Pix, decimal taxa = 0, int? parcelas = null)
    {
        db.OrdensServico.Add(ordem);
        await Integracao(db).PrepararRecebimentoIntegralAsync(Evento(ordem), forma, taxa, parcelas,
            null, _usuarioId, default);
    }

    private IntegracaoFinanceiroOrdensServico Integracao(DetaraDbContext db) =>
        new(new FinanceiroRepositorio(db), new PlataformaFinanceiroConsulta(db), new ConversorFusoHorario());

    private OrdemServicoFinalizadaFinanceiro Evento(OrdemServico ordem) =>
        new(_empresaId, ordem.Id, ordem.Codigo, ordem.ClienteId, ordem.ClienteNomeSnapshot,
            ordem.VeiculoId, ordem.VeiculoDescricaoSnapshot, ordem.VeiculoPlacaSnapshot,
            ordem.SubtotalAutorizado, 0, 0, ordem.TotalAutorizado, ordem.ConcluidaEmUtc!.Value);

    private OrdemServico Ordem(Guid solicitacaoId) => OrdemServico.RegistrarAtendimentoRealizado(
        _empresaId, DateTime.UtcNow.Year, new(Guid.NewGuid(), "Cliente QA", null, null, null, null, null),
        [new(TipoItemOrcamento.Servico, Guid.NewGuid(), null, null, "Lavagem", null, 150, 1, 1,
            OrigemComercialOrdemServico.AcordoDireto, DateTime.UtcNow, _usuarioId, null)],
        _usuarioId, DateTime.UtcNow.AddDays(-1), solicitacaoId, new string('A', 64));

    private DetaraDbContext Contexto() => new(_options, new UsuarioTeste(_empresaId));

    private sealed class UsuarioTeste(Guid empresaId, bool autenticado = true) : IUsuarioContexto
    {
        public Guid EmpresaId => empresaId;
        public Guid UsuarioId => Guid.Empty;
        public bool EstaAutenticado => autenticado;
    }
}
