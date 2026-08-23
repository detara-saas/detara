using Detara.Application;
using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Application.Autenticacao;
using Detara.Application.Dashboard;
using Detara.Application.Onboarding;
using Detara.Contracts.Autorizacao;
using Detara.Domain.Atendimento;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Agenda;
using Detara.Infrastructure.Atendimento;
using Detara.Infrastructure.Autenticacao;
using Detara.Infrastructure.Catalogo;
using Detara.Infrastructure.Clientes;
using Detara.Infrastructure.Demo;
using Detara.Infrastructure.Financeiro;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Demo;

public sealed class DemoBootstrapTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly RelogioTeste _relogio = new(new DateTimeOffset(2026, 8, 21, 15, 0, 0, TimeSpan.Zero));
    private readonly string _senhaTeste = $"Local-{Guid.NewGuid():N}";
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
    public void Guard_PermiteSomenteDevelopmentESemBypass()
    {
        DemoBootstrapPolicy.ExigirDevelopment("Development");

        var production = Assert.Throws<InvalidOperationException>(() =>
            DemoBootstrapPolicy.ExigirDevelopment("Production"));
        var staging = Assert.Throws<InvalidOperationException>(() =>
            DemoBootstrapPolicy.ExigirDevelopment("Staging"));
        var ausente = Assert.Throws<InvalidOperationException>(() =>
            DemoBootstrapPolicy.ExigirDevelopment(null));

        Assert.Equal(DemoBootstrapPolicy.MensagemSomenteDevelopment, production.Message);
        Assert.Equal(DemoBootstrapPolicy.MensagemSomenteDevelopment, staging.Message);
        Assert.Equal(DemoBootstrapPolicy.MensagemSomenteDevelopment, ausente.Message);
        Assert.Throws<InvalidOperationException>(() =>
            DemoBootstrapPolicy.ExigirConfirmacao(false));
    }

    [Fact]
    public async Task Create_EhIdempotenteEConstroiCenarioCoerenteSemNotificacao()
    {
        var servico = CriarServico();

        var primeira = await servico.CriarAsync(_senhaTeste);
        var segunda = await servico.CriarAsync(_senhaTeste);

        Assert.False(primeira.JaExistia);
        Assert.True(segunda.JaExistia);
        AssertStatusEsperado(primeira.Status);
        Assert.Equal(primeira.Status, segunda.Status);

        await using var db = CriarContexto(new ContextoTeste(primeira.Status.EmpresaId!.Value));
        var empresa = await db.Empresas.AsNoTracking()
            .SingleAsync(item => item.Id == primeira.Status.EmpresaId.Value);
        Assert.Equal(DemoBootstrapService.NomeEmpresa, empresa.NomeFantasia);
        Assert.Equal("contato@prime-detail.local", empresa.Email);
        Assert.Equal(DemoBootstrapService.FusoHorario, empresa.FusoHorario);

        var perfis = await db.Perfis.Include(item => item.Permissoes).ToListAsync();
        var administrador = perfis.Single(item => item.Nome == "Administrador");
        var recepcao = perfis.Single(item => item.Nome == "Recepção");
        var operacao = perfis.Single(item => item.Nome == "Operação");
        Assert.Equal(Permissoes.Todas.Count, administrador.Permissoes.Count);
        Assert.DoesNotContain(recepcao.Permissoes, item => item.Codigo == Permissoes.FinanceiroEditar);
        Assert.DoesNotContain(operacao.Permissoes, item => item.Codigo == Permissoes.AdministracaoUsuario);
        Assert.Equal(2, await db.Usuarios.CountAsync(item => !item.EhAtivo));
        var semPlaca = Assert.Single(await db.Veiculos.AsNoTracking()
            .Where(item => item.Placa == null).ToListAsync());
        Assert.Equal(TipoVeiculo.MotoAquatica, semPlaca.Tipo);
        Assert.Equal("Sea-Doo", semPlaca.Marca);
        Assert.Equal("GTX 300", semPlaca.Modelo);
        Assert.Equal("DEMO-JET-01", semPlaca.IdentificacaoAlternativa);

        var configuracao = await db.ConfiguracoesNotificacaoEmpresa.SingleAsync();
        Assert.False(configuracao.EnviarVeiculoProntoAutomaticamente);
        Assert.Empty(await db.NotificacoesEmail.ToListAsync());

        var contas = await db.ContasReceber.Include(item => item.Pagamentos).ToListAsync();
        Assert.Equal(3, contas.Select(item => item.OrdemServicoId).Distinct().Count());
        Assert.All(contas, conta => Assert.InRange(conta.ValorRecebido, 0, conta.ValorOriginal));
        Assert.Contains(contas, conta => conta.ValorEmAberto == conta.ValorOriginal);
        Assert.Contains(contas, conta => conta.Pagamentos.Count == 1 && conta.ValorEmAberto == 0);
        Assert.Contains(contas, conta => conta.Pagamentos.Count == 2 && conta.ValorEmAberto == 0);

        var ordemDeOrcamento = await db.OrdensServico
            .Include(item => item.Itens)
            .FirstAsync(item => item.OrcamentoOrigemId != null);
        var orcamento = await db.Orcamentos
            .Include(item => item.Itens)
            .SingleAsync(item => item.Id == ordemDeOrcamento.OrcamentoOrigemId);
        Assert.Equal(
            orcamento.Itens.Select(item => (item.NomeSnapshot, item.ValorUnitario)),
            ordemDeOrcamento.Itens.Select(item => (item.NomeSnapshot, item.ValorUnitarioAutorizado)));

        var dashboard = await new ObterDashboardOperacionalHandler(
            new ContextoTeste(primeira.Status.EmpresaId.Value),
            new PlataformaDashboardConsulta(db),
            new AgendaDashboardConsulta(db),
            new AtendimentoDashboardConsulta(db),
            new FinanceiroDashboardConsulta(db),
            new ConversorFusoHorario(),
            _relogio).Handle(
                new ObterDashboardOperacionalQuery(new(true, true, true, true)),
                CancellationToken.None);
        Assert.Equal(2, dashboard.Agenda?.AgendamentosHoje);
        Assert.Equal(1, dashboard.Atendimento?.OrdensEmExecucao);
        Assert.Equal(2, dashboard.Atendimento?.OrdensAguardandoRetirada);
        Assert.Equal(1, dashboard.Atendimento?.OrcamentosEmAberto);
        Assert.Equal(1390m, dashboard.Financeiro?.RecebidoBruto);
        Assert.Equal(18m, dashboard.Financeiro?.Taxas);
        Assert.Equal(1, dashboard.Financeiro?.ContasPendentes);
        Assert.Equal(450m, dashboard.Financeiro?.ValorPendente);
    }

    [Fact]
    public async Task Reset_AfetaSomenteDemoEPreservaTenantNormal()
    {
        var empresaNormal = new Empresa(
            "Tenant Normal",
            "Tenant Normal Ltda.",
            "88888888000188",
            "tenant-normal");
        await using (var sistema = CriarContexto(ContextoTeste.Anonimo))
        {
            sistema.Empresas.Add(empresaNormal);
            await sistema.SaveChangesAsync();
        }

        await using (var normal = CriarContexto(new ContextoTeste(empresaNormal.Id)))
        {
            normal.Clientes.Add(new Cliente(
                empresaNormal.Id,
                "Cliente preservado",
                TipoPessoa.PessoaFisica,
                null,
                "1100000099",
                null,
                "preservado@example.com",
                null,
                null));
            await normal.SaveChangesAsync();
        }

        var servico = CriarServico();
        var criado = await servico.CriarAsync(_senhaTeste);
        var empresaDemoId = criado.Status.EmpresaId!.Value;
        await servico.ResetarAsync(_senhaTeste);

        await using var verificacaoNormal = CriarContexto(new ContextoTeste(empresaNormal.Id));
        Assert.Single(await verificacaoNormal.Clientes.ToListAsync());
        Assert.Empty(await verificacaoNormal.Clientes
            .Where(item => item.EmpresaId == empresaDemoId)
            .ToListAsync());
        await using var sistemaFinal = CriarContexto(ContextoTeste.Anonimo);
        Assert.NotNull(await sistemaFinal.Empresas.SingleOrDefaultAsync(item => item.Id == empresaNormal.Id));
        Assert.NotNull(await sistemaFinal.Empresas.SingleOrDefaultAsync(item => item.Id == empresaDemoId));
    }

    [Fact]
    public async Task Demo_ConcluiOnboardingEAdminAutenticaSemSelecaoMultiempresa()
    {
        var resultado = await CriarServico().CriarAsync(_senhaTeste);
        var empresaId = resultado.Status.EmpresaId!.Value;
        await using var tenant = CriarContexto(new ContextoTeste(empresaId));
        var onboarding = await new ObterOnboardingEmpresaHandler(
            new ContextoTeste(empresaId),
            new PlataformaOnboardingConsulta(tenant),
            new AtendimentoOnboardingConsulta(tenant),
            new CatalogoOnboardingConsulta(tenant),
            new ClientesOnboardingConsulta(tenant),
            new AgendaOnboardingConsulta(tenant)).Handle(
                new ObterOnboardingEmpresaQuery(new(true, true, true, true, true)),
                CancellationToken.None);

        Assert.True(onboarding.Concluido);
        Assert.Equal(onboarding.QuantidadeTotal, onboarding.QuantidadeConcluida);

        await using var loginDb = CriarContexto(ContextoTeste.Anonimo);
        var autenticacao = await new AutenticarCommandHandler(
            new UsuarioAutenticacaoRepositorio(loginDb),
            new SenhaServico(new PasswordHasher<Usuario>()),
            new TokenTeste(),
            new ChallengeTeste()).Handle(
                new AutenticarCommand(DemoBootstrapService.EmailAdministrador, _senhaTeste),
                CancellationToken.None);

        var sessao = Assert.IsType<SessaoTenantResultado>(autenticacao);
        Assert.Equal(empresaId, sessao.EmpresaId);
        Assert.Equal("Administrador", sessao.Perfil);
    }

    [Fact]
    public async Task Reset_RecalculaDatasDaAgendaRelativasAoNovoMomento()
    {
        var servico = CriarServico();
        var resultado = await servico.CriarAsync(_senhaTeste);
        var primeiraData = await ObterPrimeiraDataAgendaAsync(resultado.Status.EmpresaId!.Value);

        _relogio.Avancar(TimeSpan.FromDays(2));
        await servico.ResetarAsync(_senhaTeste);
        var segundaData = await ObterPrimeiraDataAgendaAsync(resultado.Status.EmpresaId.Value);

        Assert.Equal(primeiraData.AddDays(2), segundaData);
    }

    private async Task<DateOnly> ObterPrimeiraDataAgendaAsync(Guid empresaId)
    {
        await using var db = CriarContexto(new ContextoTeste(empresaId));
        var primeiro = await db.Agendamentos.OrderBy(item => item.InicioUtc).FirstAsync();
        var conversor = new ConversorFusoHorario();
        return DateOnly.FromDateTime(conversor.ParaLocal(
            primeiro.InicioUtc,
            DemoBootstrapService.FusoHorario));
    }

    private static void AssertStatusEsperado(DemoBootstrapStatus status)
    {
        Assert.True(status.Encontrada);
        Assert.Equal(1, status.Empresas);
        Assert.Equal(3, status.Perfis);
        Assert.Equal(3, status.Usuarios);
        Assert.Equal(9, status.Clientes);
        Assert.Equal(9, status.Veiculos);
        Assert.Equal(5, status.Categorias);
        Assert.Equal(10, status.Servicos);
        Assert.Equal(0, status.Pacotes);
        Assert.Equal(7, status.Agendamentos);
        Assert.Equal(4, status.Orcamentos);
        Assert.Equal(4, status.OrdensServico);
        Assert.Equal(3, status.ContasReceber);
        Assert.Equal(3, status.Pagamentos);
        Assert.Equal(0, status.Notificacoes);
    }

    private DemoBootstrapService CriarServico() => new(
        _options,
        new PasswordHasher<Usuario>(),
        _relogio);

    private DetaraDbContext CriarContexto(IUsuarioContexto contexto) => new(_options, contexto);

    private sealed class ContextoTeste(Guid empresaId, Guid? usuarioId = null) : IUsuarioContexto
    {
        public static ContextoTeste Anonimo { get; } = new(Guid.Empty, Guid.Empty);
        public Guid UsuarioId { get; } = usuarioId ?? Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => EmpresaId != Guid.Empty;
    }

    private sealed class RelogioTeste(DateTimeOffset agora) : TimeProvider
    {
        private DateTimeOffset _agora = agora;
        public override DateTimeOffset GetUtcNow() => _agora;
        public void Avancar(TimeSpan periodo) => _agora = _agora.Add(periodo);
    }

    private sealed class TokenTeste : ITokenServico
    {
        public TokenGerado Gerar(CandidatoLoginTenant candidato) =>
            new("token-demo", DateTime.UtcNow.AddMinutes(5));
    }

    private sealed class ChallengeTeste : IChallengeSelecaoEmpresaTenant
    {
        public ChallengeSelecaoEmpresaCriado Criar(
            IReadOnlyCollection<MembershipLoginTenantAutorizada> memberships) =>
            new("challenge", DateTime.UtcNow.AddMinutes(5));

        public IReadOnlyCollection<MembershipLoginTenantAutorizada> Validar(string challenge) => [];
    }
}
