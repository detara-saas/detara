using Detara.Application.Abstracoes;
using Detara.Application.Onboarding;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Agenda;
using Detara.Infrastructure.Atendimento;
using Detara.Infrastructure.Catalogo;
using Detara.Infrastructure.Clientes;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Onboarding;

public sealed class OnboardingEmpresaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using var db = new DetaraDbContext(_options, ContextoUsuario.Anonimo);
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task EmpresaNova_ReconheceProvisionamentoSemInventarProgressoOperacional()
    {
        var empresa = await CriarEmpresaAsync("empresa-nova");
        await using var db = CriarContexto(empresa.Id);

        var resultado = await CriarHandler(db, empresa.Id).Handle(
            new ObterOnboardingEmpresaQuery(SemPermissoes),
            CancellationToken.None);

        Assert.False(resultado.Concluido);
        Assert.Equal(1, resultado.QuantidadeConcluida);
        Assert.Equal(5, resultado.QuantidadeTotal);
        var etapaEmpresa = resultado.Etapas.Single(x => x.Codigo == "empresa");
        Assert.True(etapaEmpresa.Concluida);
        Assert.False(etapaEmpresa.PodeExecutar);
        Assert.Null(etapaEmpresa.Destino);
        Assert.All(
            resultado.Etapas.Where(x => x.Codigo != "empresa"),
            etapa => Assert.False(etapa.Concluida));
    }

    [Fact]
    public async Task DadosReaisAtualizamEtapasSemComandoDeConclusao()
    {
        var empresa = await CriarEmpresaAsync("empresa-completa");
        await PopularOperacaoCompletaAsync(empresa.Id, incluirAgendamentoValido: true);
        await using var db = CriarContexto(empresa.Id);

        var resultado = await CriarHandler(db, empresa.Id).Handle(
            new ObterOnboardingEmpresaQuery(TodasPermissoes),
            CancellationToken.None);

        Assert.True(resultado.Concluido);
        Assert.Equal(5, resultado.QuantidadeConcluida);
        Assert.All(resultado.Etapas, etapa => Assert.True(etapa.Concluida));
    }

    [Fact]
    public async Task AgendamentoCanceladoNaoRepresentaOperacaoIniciada()
    {
        var empresa = await CriarEmpresaAsync("empresa-cancelada");
        await PopularOperacaoCompletaAsync(empresa.Id, incluirAgendamentoValido: false);
        await using var db = CriarContexto(empresa.Id);

        var resultado = await CriarHandler(db, empresa.Id).Handle(
            new ObterOnboardingEmpresaQuery(TodasPermissoes),
            CancellationToken.None);

        Assert.False(resultado.Concluido);
        Assert.False(resultado.Etapas.Single(x => x.Codigo == "agenda").Concluida);
        Assert.Equal(4, resultado.QuantidadeConcluida);
    }

    [Fact]
    public async Task TenantANaoObservaProgressoCompletoDoTenantB()
    {
        var empresaA = await CriarEmpresaAsync("empresa-a");
        var empresaB = await CriarEmpresaAsync("empresa-b");
        await PopularOperacaoCompletaAsync(empresaB.Id, incluirAgendamentoValido: true);
        await using var dbA = CriarContexto(empresaA.Id);

        var resultadoA = await CriarHandler(dbA, empresaA.Id).Handle(
            new ObterOnboardingEmpresaQuery(TodasPermissoes),
            CancellationToken.None);

        Assert.Equal(1, resultadoA.QuantidadeConcluida);
        Assert.All(
            resultadoA.Etapas.Where(x => x.Codigo != "empresa"),
            etapa => Assert.False(etapa.Concluida));
    }

    [Fact]
    public async Task EtapaPendenteSemPermissaoPermaneceInformativa()
    {
        var empresa = await CriarEmpresaAsync("empresa-sem-permissao");
        await using var db = CriarContexto(empresa.Id);

        var resultado = await CriarHandler(db, empresa.Id).Handle(
            new ObterOnboardingEmpresaQuery(SemPermissoes),
            CancellationToken.None);

        var catalogo = resultado.Etapas.Single(x => x.Codigo == "catalogo");
        Assert.False(catalogo.Concluida);
        Assert.False(catalogo.PodeExecutar);
        Assert.Equal("/servicos/novo", catalogo.Destino);
    }

    private async Task<Empresa> CriarEmpresaAsync(string slug)
    {
        var empresa = new Empresa(
            $"Empresa {slug}",
            $"Empresa {slug} Ltda",
            Guid.NewGuid().ToString("N")[..14],
            slug);
        await using var db = new DetaraDbContext(_options, ContextoUsuario.Anonimo);
        db.Empresas.Add(empresa);
        await db.SaveChangesAsync();
        return empresa;
    }

    private async Task PopularOperacaoCompletaAsync(
        Guid empresaId,
        bool incluirAgendamentoValido)
    {
        await using var db = CriarContexto(empresaId);
        var categoria = new CategoriaServico(empresaId, "Lavagem", null, 0);
        var cliente = new Cliente(
            empresaId,
            "Cliente Beta",
            TipoPessoa.PessoaFisica,
            null,
            null,
            null,
            null,
            null,
            null);
        db.AddRange(
            categoria,
            cliente,
            new ConfiguracaoOperacionalAtendimento(
                empresaId,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado));
        await db.SaveChangesAsync();

        var servico = new Servico(
            empresaId,
            categoria.Id,
            "Lavagem técnica",
            null,
            TipoPrecificacao.APartirDe,
            100m,
            90,
            0);
        var veiculo = new Veiculo(
            empresaId,
            cliente.Id,
            "ABC1D23",
            "Honda",
            "Civic",
            null,
            2024,
            2024,
            null,
            null,
            null);
        db.AddRange(servico, veiculo);
        await db.SaveChangesAsync();

        var agendamento = new Agendamento(
            empresaId,
            cliente.Id,
            cliente.Nome,
            veiculo.Id,
            "Honda Civic",
            veiculo.Placa,
            DateTime.UtcNow.AddDays(1),
            90,
            null,
            null,
            [
                new ItemAgendamentoSnapshot(
                    TipoItemAgendamento.Servico,
                    servico.Id,
                    servico.Nome,
                    servico.Descricao,
                    servico.TipoPrecificacao,
                    servico.PrecoBase,
                    servico.DuracaoEstimadaMinutos)
            ]);
        if (!incluirAgendamentoValido)
        {
            agendamento.AlterarStatus(StatusAgendamento.Cancelado, "Cancelado no teste");
        }

        db.Agendamentos.Add(agendamento);
        await db.SaveChangesAsync();
    }

    private DetaraDbContext CriarContexto(Guid empresaId) =>
        new(_options, new ContextoUsuario(empresaId));

    private static ObterOnboardingEmpresaHandler CriarHandler(
        DetaraDbContext db,
        Guid empresaId) =>
        new(
            new ContextoUsuario(empresaId),
            new PlataformaOnboardingConsulta(db),
            new AtendimentoOnboardingConsulta(db),
            new CatalogoOnboardingConsulta(db),
            new ClientesOnboardingConsulta(db),
            new AgendaOnboardingConsulta(db));

    private static readonly PermissoesAcoesOnboarding SemPermissoes =
        new(false, false, false, false, false);

    private static readonly PermissoesAcoesOnboarding TodasPermissoes =
        new(true, true, true, true, true);

    private sealed class ContextoUsuario(Guid empresaId, bool autenticado = true) : IUsuarioContexto
    {
        public static ContextoUsuario Anonimo { get; } = new(Guid.Empty, false);
        public Guid UsuarioId { get; } = autenticado ? Guid.NewGuid() : Guid.Empty;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = autenticado;
    }
}
