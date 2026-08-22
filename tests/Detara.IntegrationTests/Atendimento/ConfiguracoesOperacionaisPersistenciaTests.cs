using Detara.Application.Abstracoes;
using Detara.Application.Atendimento;
using Detara.Domain.Atendimento;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Atendimento;

public sealed class ConfiguracoesOperacionaisPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _empresaA;
    private Guid _empresaB;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        var empresaA = new Empresa("Empresa A", "Empresa A Ltda", "11111111000111", "empresa-a");
        var empresaB = new Empresa("Empresa B", "Empresa B Ltda", "22222222000122", "empresa-b");
        _empresaA = empresaA.Id;
        _empresaB = empresaB.Id;
        await using var sistema = new DetaraDbContext(_options, UsuarioContextoTeste.Anonimo);
        await sistema.Database.EnsureCreatedAsync();
        sistema.Empresas.AddRange(empresaA, empresaB);
        await sistema.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task EmpresaSemConfiguracao_RetornaDefaultsSemCriarRegistro()
    {
        await using var context = Contexto(_empresaA);
        var resultado = await new ObterConfiguracaoOperacionalHandler(
            new ConfiguracoesOperacionaisRepositorio(context))
            .Handle(new ObterConfiguracaoOperacionalQuery(), default);

        Assert.Null(resultado.Id);
        Assert.Equal(NivelExigenciaOperacional.Desabilitado, resultado.ChecklistEntrada);
        Assert.Equal(NivelExigenciaOperacional.Desabilitado, resultado.FotosEntrada);
        Assert.Equal(NivelExigenciaOperacional.Desabilitado, resultado.FotosSaida);
        Assert.Null(resultado.Checklist.Id);
        Assert.Empty(resultado.Checklist.Itens);
        Assert.Equal(0, await context.ConfiguracoesOperacionaisAtendimento.CountAsync());
        Assert.Equal(0, await context.ChecklistModelos.CountAsync());
    }

    [Fact]
    public async Task PrimeiroSave_CriaConfiguracaoNoTenantAtual()
    {
        await using var context = Contexto(_empresaA);
        var resultado = await AtualizarConfiguracao(context).Handle(
            new AtualizarConfiguracaoOperacionalCommand(
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Opcional,
                NivelExigenciaOperacional.Obrigatorio),
            default);

        Assert.NotNull(resultado.Id);
        var persistida = await context.ConfiguracoesOperacionaisAtendimento.SingleAsync();
        Assert.Equal(_empresaA, persistida.EmpresaId);
        Assert.Equal(NivelExigenciaOperacional.Opcional, persistida.FotosEntrada);
        Assert.Equal(NivelExigenciaOperacional.Obrigatorio, persistida.FotosSaida);
    }

    [Theory]
    [InlineData(NivelExigenciaOperacional.Opcional)]
    [InlineData(NivelExigenciaOperacional.Obrigatorio)]
    public async Task ChecklistHabilitadoSemItens_EhRejeitado(NivelExigenciaOperacional nivel)
    {
        await using var context = Contexto(_empresaA);

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            AtualizarConfiguracao(context).Handle(
                new AtualizarConfiguracaoOperacionalCommand(
                    nivel,
                    NivelExigenciaOperacional.Desabilitado,
                    NivelExigenciaOperacional.Desabilitado),
                default));

        Assert.Equal(0, await context.ConfiguracoesOperacionaisAtendimento.CountAsync());
    }

    [Fact]
    public async Task DesabilitarChecklist_PreservaHistoricoENaoAplicaEmNovaOperacao_EReativarDisponibiliza()
    {
        await using var context = Contexto(_empresaA);
        await AtualizarChecklist(context).Handle(
            new AtualizarChecklistModeloCommand(
                ChecklistModelo.NomePadrao,
                "Vistoria visual",
                ["Riscos aparentes", "Rodas danificadas"]),
            default);
        context.ChangeTracker.Clear();
        await AtualizarConfiguracao(context).Handle(
            new AtualizarConfiguracaoOperacionalCommand(
                NivelExigenciaOperacional.Obrigatorio,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado),
            default);
        var usuarioId = Guid.NewGuid();
        var ordemHistorica = CriarOrdem(usuarioId);
        ordemHistorica.RealizarCheckIn(
            new(
                NivelExigenciaOperacional.Obrigatorio,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                ChecklistModelo.NomePadrao,
                ["Riscos aparentes", "Rodas danificadas"]),
            null,
            null,
            usuarioId);
        context.OrdensServico.Add(ordemHistorica);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        await AtualizarConfiguracao(context).Handle(
            new AtualizarConfiguracaoOperacionalCommand(
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado),
            default);

        Assert.Equal(2, await context.ChecklistModeloItens.CountAsync());
        Assert.Equal(
            NivelExigenciaOperacional.Desabilitado,
            (await context.ConfiguracoesOperacionaisAtendimento.SingleAsync()).ChecklistEntrada);
        var historico = await context.OrdensServico
            .Include(item => item.Checklist)
            .ThenInclude(item => item!.Itens)
            .SingleAsync(item => item.Id == ordemHistorica.Id);
        Assert.Equal(2, historico.Checklist?.Itens.Count);

        var ordemSemChecklist = CriarOrdem(usuarioId);
        ordemSemChecklist.RealizarCheckIn(
            new(
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                ChecklistModelo.NomePadrao,
                ["Riscos aparentes", "Rodas danificadas"]),
            null,
            null,
            usuarioId);
        Assert.Null(ordemSemChecklist.Checklist);

        context.ChangeTracker.Clear();
        await AtualizarConfiguracao(context).Handle(
            new AtualizarConfiguracaoOperacionalCommand(
                NivelExigenciaOperacional.Opcional,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado),
            default);
        var ordemReativada = CriarOrdem(usuarioId);
        ordemReativada.RealizarCheckIn(
            new(
                NivelExigenciaOperacional.Opcional,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                ChecklistModelo.NomePadrao,
                ["Riscos aparentes", "Rodas danificadas"]),
            null,
            null,
            usuarioId);

        Assert.NotNull(ordemReativada.Checklist);
        Assert.Equal(
            NivelExigenciaOperacional.Opcional,
            (await context.ConfiguracoesOperacionaisAtendimento.SingleAsync()).ChecklistEntrada);
    }

    [Fact]
    public async Task ChecklistPersistido_DevolveItensOrdenados()
    {
        await using var context = Contexto(_empresaA);
        var resultado = await AtualizarChecklist(context).Handle(
            new AtualizarChecklistModeloCommand(
                "Entrada",
                null,
                ["Vidros", "Pintura", "Rodas"]),
            default);

        Assert.Equal([1, 2, 3], resultado.Checklist.Itens.Select(item => item.Ordem));
        Assert.Equal(["Vidros", "Pintura", "Rodas"], resultado.Checklist.Itens.Select(item => item.Descricao));
    }

    [Fact]
    public async Task UmRegistroPorEmpresa_EhGarantidoNoBanco()
    {
        await using var context = Contexto(_empresaA);
        context.ConfiguracoesOperacionaisAtendimento.AddRange(
            new ConfiguracaoOperacionalAtendimento(
                _empresaA,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado),
            new ConfiguracaoOperacionalAtendimento(
                _empresaA,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Opcional,
                NivelExigenciaOperacional.Desabilitado));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    [Fact]
    public async Task EmpresaA_NaoConsultaConfiguracaoOuChecklistEmpresaB()
    {
        await CriarDadosEmpresaBAsync();
        await using var contextA = Contexto(_empresaA);

        Assert.Empty(await contextA.ConfiguracoesOperacionaisAtendimento.ToArrayAsync());
        Assert.Empty(await contextA.ChecklistModelos.ToArrayAsync());
        Assert.Empty(await contextA.ChecklistModeloItens.ToArrayAsync());
    }

    [Fact]
    public async Task EmpresaA_NaoAtualizaConfiguracaoEmpresaB()
    {
        await CriarDadosEmpresaBAsync();
        await using var contextA = Contexto(_empresaA);
        var configuracaoB = await contextA.ConfiguracoesOperacionaisAtendimento
            .IgnoreQueryFilters()
            .SingleAsync(item => item.EmpresaId == _empresaB);
        configuracaoB.Atualizar(
            NivelExigenciaOperacional.Obrigatorio,
            NivelExigenciaOperacional.Obrigatorio,
            NivelExigenciaOperacional.Obrigatorio);

        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => contextA.SaveChangesAsync());
    }

    [Fact]
    public async Task EmpresaA_NaoAtualizaChecklistEmpresaB()
    {
        await CriarDadosEmpresaBAsync();
        await using var contextA = Contexto(_empresaA);
        var checklistB = await contextA.ChecklistModelos
            .IgnoreQueryFilters()
            .Include(item => item.Itens)
            .SingleAsync(item => item.EmpresaId == _empresaB);
        checklistB.Atualizar("Tentativa", null, ["Item adulterado"]);

        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => contextA.SaveChangesAsync());
    }

    private async Task CriarDadosEmpresaBAsync()
    {
        await using var contextB = Contexto(_empresaB);
        contextB.ConfiguracoesOperacionaisAtendimento.Add(new ConfiguracaoOperacionalAtendimento(
            _empresaB,
            NivelExigenciaOperacional.Opcional,
            NivelExigenciaOperacional.Opcional,
            NivelExigenciaOperacional.Opcional));
        contextB.ChecklistModelos.Add(new ChecklistModelo(
            _empresaB,
            ChecklistModelo.NomePadrao,
            null,
            ["Item B"]));
        await contextB.SaveChangesAsync();
    }

    private OrdemServico CriarOrdem(Guid usuarioId) => new(
        _empresaA,
        2026,
        new(
            Guid.NewGuid(),
            "Cliente teste",
            null,
            null,
            Guid.NewGuid(),
            "Honda Civic",
            "ABC1D23"),
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
            "Serviço teste",
            null,
            100,
            1,
            1,
            OrigemComercialOrdemServico.AcordoDireto,
            DateTime.UtcNow,
            usuarioId,
            null)],
        usuarioId,
        DateTime.UtcNow);

    private AtualizarConfiguracaoOperacionalHandler AtualizarConfiguracao(DetaraDbContext context) =>
        new(new UsuarioContextoTeste(_empresaA), new ConfiguracoesOperacionaisRepositorio(context));

    private AtualizarChecklistModeloHandler AtualizarChecklist(DetaraDbContext context) =>
        new(new UsuarioContextoTeste(_empresaA), new ConfiguracoesOperacionaisRepositorio(context));

    private DetaraDbContext Contexto(Guid empresaId) =>
        new(_options, new UsuarioContextoTeste(empresaId));

    private sealed class UsuarioContextoTeste(Guid empresaId) : IUsuarioContexto
    {
        public static UsuarioContextoTeste Anonimo { get; } = new(Guid.Empty, false);
        private UsuarioContextoTeste(Guid empresaId, bool autenticado)
            : this(empresaId) => EstaAutenticado = autenticado;
        public Guid UsuarioId { get; } = Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = empresaId != Guid.Empty;
    }
}
