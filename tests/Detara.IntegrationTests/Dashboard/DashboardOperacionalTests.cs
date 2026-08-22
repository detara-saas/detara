using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Application.Dashboard;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Agenda;
using Detara.Infrastructure.Atendimento;
using Detara.Infrastructure.Financeiro;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Dashboard;

public sealed class DashboardOperacionalTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Agora =
        new(2026, 8, 21, 15, 0, 0, TimeSpan.Zero);
    private static readonly PermissoesDashboardOperacional TodasPermissoes =
        new(true, true, true, true);
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using var db = CriarContexto(ContextoTeste.Anonimo);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task EmpresaSemDados_RetornaZerosEListasVazias()
    {
        var empresa = await CriarEmpresaAsync("dashboard-vazio");

        var resultado = await ObterDashboardAsync(empresa.Id, TodasPermissoes);

        Assert.Equal(new DateOnly(2026, 8, 21), resultado.DataReferencia);
        Assert.NotNull(resultado.Agenda);
        Assert.Equal(0, resultado.Agenda.AgendamentosHoje);
        Assert.Empty(resultado.Agenda.Itens);
        Assert.NotNull(resultado.Atendimento);
        Assert.Equal(0, resultado.Atendimento.OrdensEmExecucao);
        Assert.Equal(0, resultado.Atendimento.OrdensAguardandoRetirada);
        Assert.Equal(0, resultado.Atendimento.OrcamentosEmAberto);
        Assert.NotNull(resultado.Financeiro);
        Assert.Equal(0, resultado.Financeiro.RecebidoBruto);
        Assert.Equal(0, resultado.Financeiro.ContasPendentes);
    }

    [Fact]
    public async Task TenantAB_IsolaMetricasEUsaSomenteEstadosEPagamentosReais()
    {
        var empresaA = await CriarEmpresaAsync("dashboard-a");
        var empresaB = await CriarEmpresaAsync("dashboard-b");
        await PopularTenantAAsync(empresaA.Id);
        await PopularTenantBAsync(empresaB.Id);

        var dashboardA = await ObterDashboardAsync(empresaA.Id, TodasPermissoes);
        var dashboardB = await ObterDashboardAsync(empresaB.Id, TodasPermissoes);

        Assert.NotNull(dashboardA.Agenda);
        Assert.Equal(1, dashboardA.Agenda.AgendamentosHoje);
        Assert.Single(dashboardA.Agenda.Itens);
        Assert.DoesNotContain(
            dashboardA.Agenda.Itens,
            item => item.Status is StatusAgendamento.Cancelado or StatusAgendamento.NaoCompareceu);
        Assert.NotNull(dashboardA.Atendimento);
        Assert.Equal(1, dashboardA.Atendimento.OrdensEmExecucao);
        Assert.Equal(0, dashboardA.Atendimento.OrdensAguardandoRetirada);
        Assert.Equal(1, dashboardA.Atendimento.OrcamentosEmAberto);
        Assert.NotNull(dashboardA.Financeiro);
        Assert.Equal(400m, dashboardA.Financeiro.RecebidoBruto);
        Assert.Equal(10m, dashboardA.Financeiro.Taxas);
        Assert.Equal(1, dashboardA.Financeiro.ContasPendentes);
        Assert.Equal(600m, dashboardA.Financeiro.ValorPendente);

        Assert.NotNull(dashboardB.Agenda);
        Assert.Equal(5, dashboardB.Agenda.AgendamentosHoje);
        Assert.Equal(3, dashboardB.Atendimento?.OrdensEmExecucao);
        Assert.Equal(1, dashboardB.Atendimento?.OrdensAguardandoRetirada);
        Assert.Equal(0, dashboardB.Atendimento?.OrcamentosEmAberto);
        Assert.Equal(150m, dashboardB.Financeiro?.RecebidoBruto);
        Assert.Equal(0, dashboardB.Financeiro?.ContasPendentes);
    }

    [Fact]
    public async Task UsuarioSemFinanceiro_NaoRecebeValoresMesmoQuandoTenantPossuiDados()
    {
        var empresa = await CriarEmpresaAsync("dashboard-sem-financeiro");
        await PopularTenantAAsync(empresa.Id);

        var resultado = await ObterDashboardAsync(
            empresa.Id,
            new(true, true, true, false));

        Assert.NotNull(resultado.Agenda);
        Assert.NotNull(resultado.Atendimento);
        Assert.Null(resultado.Financeiro);
    }

    private async Task PopularTenantAAsync(Guid empresaId)
    {
        await using var db = CriarContexto(new ContextoTeste(empresaId));
        db.Agendamentos.Add(CriarAgendamento(empresaId, 9, StatusAgendamento.Confirmado));
        db.Agendamentos.Add(CriarAgendamento(empresaId, 10, StatusAgendamento.Cancelado));
        db.Agendamentos.Add(CriarAgendamento(empresaId, 11, StatusAgendamento.NaoCompareceu));
        db.OrdensServico.Add(CriarOrdem(empresaId, StatusOrdemServico.EmExecucao));
        db.OrdensServico.Add(CriarOrdem(empresaId, StatusOrdemServico.Aberta));
        db.Orcamentos.Add(CriarOrcamento(empresaId, StatusOrcamento.Rascunho, new DateOnly(2026, 8, 28)));
        db.Orcamentos.Add(CriarOrcamento(empresaId, StatusOrcamento.Emitido, new DateOnly(2026, 8, 20)));
        var conta = CriarConta(empresaId, 1000m);
        conta.RegistrarPagamento(FormaPagamento.Pix, 400m, 10m, null, null,
            Agora.UtcDateTime, Guid.NewGuid());
        db.ContasReceber.Add(conta);
        await db.SaveChangesAsync();
    }

    private async Task PopularTenantBAsync(Guid empresaId)
    {
        await using var db = CriarContexto(new ContextoTeste(empresaId));
        for (var indice = 0; indice < 5; indice++)
        {
            db.Agendamentos.Add(CriarAgendamento(
                empresaId,
                8 + indice,
                StatusAgendamento.Agendado));
        }
        for (var indice = 0; indice < 3; indice++)
        {
            db.OrdensServico.Add(CriarOrdem(empresaId, StatusOrdemServico.EmExecucao));
        }
        db.OrdensServico.Add(CriarOrdem(empresaId, StatusOrdemServico.AguardandoRetirada));
        var conta = CriarConta(empresaId, 150m);
        conta.RegistrarPagamento(FormaPagamento.Pix, 150m, 0, null, null,
            Agora.UtcDateTime, Guid.NewGuid());
        db.ContasReceber.Add(conta);
        await db.SaveChangesAsync();
    }

    private async Task<DashboardOperacionalResultado> ObterDashboardAsync(
        Guid empresaId,
        PermissoesDashboardOperacional permissoes)
    {
        await using var db = CriarContexto(new ContextoTeste(empresaId));
        return await new ObterDashboardOperacionalHandler(
            new ContextoTeste(empresaId),
            new PlataformaDashboardConsulta(db),
            new AgendaDashboardConsulta(db),
            new AtendimentoDashboardConsulta(db),
            new FinanceiroDashboardConsulta(db),
            new ConversorFusoHorario(),
            new RelogioTeste(Agora)).Handle(
                new ObterDashboardOperacionalQuery(permissoes),
                CancellationToken.None);
    }

    private Agendamento CriarAgendamento(
        Guid empresaId,
        int horaLocal,
        StatusAgendamento status)
    {
        var inicioUtc = new ConversorFusoHorario().ParaUtc(
            new DateTime(2026, 8, 21, horaLocal, 0, 0),
            "America/Sao_Paulo");
        var agendamento = new Agendamento(
            empresaId,
            Guid.NewGuid(),
            $"Cliente {horaLocal}",
            Guid.NewGuid(),
            "Veículo teste",
            $"TST{horaLocal:D4}"[..7],
            inicioUtc,
            60,
            null,
            null,
            [new(
                TipoItemAgendamento.Servico,
                Guid.NewGuid(),
                "Lavagem técnica",
                null,
                TipoPrecificacao.Fixo,
                100m,
                60)]);
        if (status != StatusAgendamento.Agendado)
        {
            agendamento.AlterarStatus(status, status == StatusAgendamento.Cancelado ? "Cancelado" : null);
        }
        return agendamento;
    }

    private static OrdemServico CriarOrdem(Guid empresaId, StatusOrdemServico status)
    {
        var usuarioId = Guid.NewGuid();
        var ordem = new OrdemServico(
            empresaId,
            2026,
            new(
                Guid.NewGuid(),
                "Cliente OS",
                null,
                null,
                Guid.NewGuid(),
                "Veículo OS",
                "TST1A23"),
            OrigemOrdemServico.AtendimentoDireto,
            null,
            null,
            60,
            0,
            0,
            [new(
                TipoItemOrcamento.Personalizado,
                null,
                null,
                null,
                "Serviço autorizado",
                null,
                100m,
                1,
                1,
                OrigemComercialOrdemServico.AcordoDireto,
                Agora.UtcDateTime,
                usuarioId,
                null)],
            usuarioId,
            Agora.UtcDateTime);
        if (status == StatusOrdemServico.Aberta) return ordem;
        ordem.RealizarCheckIn(
            new(
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                null,
                []),
            null,
            null,
            usuarioId);
        ordem.IniciarExecucao(usuarioId, null);
        if (status == StatusOrdemServico.EmExecucao) return ordem;
        ordem.FinalizarExecucao(usuarioId, null);
        return ordem;
    }

    private static Orcamento CriarOrcamento(
        Guid empresaId,
        StatusOrcamento status,
        DateOnly validoAte)
    {
        var usuarioId = Guid.NewGuid();
        var orcamento = new Orcamento(
            empresaId,
            new(
                Guid.NewGuid(),
                "Cliente orçamento",
                null,
                null,
                Guid.NewGuid(),
                "Veículo orçamento",
                "ORC1A23"),
            null,
            null,
            validoAte,
            null,
            null,
            null,
            0,
            0,
            [new(
                TipoItemOrcamento.Personalizado,
                null,
                "Serviço orçamento",
                null,
                null,
                null,
                100m,
                1,
                1,
                null)],
            usuarioId);
        if (status == StatusOrcamento.Emitido)
        {
            orcamento.Emitir(2026, usuarioId);
        }
        return orcamento;
    }

    private static ContaReceber CriarConta(Guid empresaId, decimal valor) => new(
        empresaId,
        Guid.NewGuid(),
        "OS-2026-TESTE",
        Guid.NewGuid(),
        "Cliente financeiro",
        Guid.NewGuid(),
        "Veículo financeiro",
        "FIN1A23",
        valor,
        0,
        0,
        valor,
        new DateOnly(2026, 8, 21));

    private async Task<Empresa> CriarEmpresaAsync(string slug)
    {
        var empresa = new Empresa(
            $"Empresa {slug}",
            $"Empresa {slug} Ltda.",
            Guid.NewGuid().ToString("N")[..14],
            slug,
            null,
            null,
            "America/Sao_Paulo");
        await using var db = CriarContexto(ContextoTeste.Anonimo);
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync();
        return empresa;
    }

    private DetaraDbContext CriarContexto(IUsuarioContexto contexto) => new(_options, contexto);

    private sealed class ContextoTeste(Guid empresaId) : IUsuarioContexto
    {
        public static ContextoTeste Anonimo { get; } = new(Guid.Empty);
        public Guid UsuarioId { get; } = empresaId == Guid.Empty ? Guid.Empty : Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => EmpresaId != Guid.Empty;
    }

    private sealed class RelogioTeste(DateTimeOffset agora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => agora;
    }
}
