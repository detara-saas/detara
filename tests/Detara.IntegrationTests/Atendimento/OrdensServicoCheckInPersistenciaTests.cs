using Detara.Application.Abstracoes;
using Detara.Application.Atendimento;
using Detara.Domain.Atendimento;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Atendimento;

public sealed class OrdensServicoCheckInPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _empresaId;
    private readonly Guid _usuarioId = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlite(_connection)
            .Options;
        var empresa = new Empresa(
            "Empresa OS",
            "Empresa OS Ltda.",
            "11111111000111",
            "empresa-os");
        _empresaId = empresa.Id;
        await using var context = Contexto();
        await context.Database.EnsureCreatedAsync();
        context.Empresas.Add(empresa);
        await context.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task ConfiguracaoOpcional_SemCheckIn_PermiteIniciarExecucao()
    {
        await using var context = Contexto();
        context.ConfiguracoesOperacionaisAtendimento.Add(new(
            _empresaId,
            NivelExigenciaOperacional.Opcional,
            NivelExigenciaOperacional.Opcional,
            NivelExigenciaOperacional.Opcional));
        var ordem = CriarOrdem();
        context.OrdensServico.Add(ordem);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var resultado = await CriarHandler(context).Handle(
            new TransicaoOrdemServicoCommand(ordem.Id, null),
            CancellationToken.None);

        Assert.Equal(StatusOrdemServico.EmExecucao, resultado.OrdemServico.Status);
        Assert.Null(resultado.OrdemServico.CheckInEmUtc);
    }

    [Fact]
    public async Task SemConfiguracao_MantemCheckInObrigatorioPorCompatibilidade()
    {
        await using var context = Contexto();
        var ordem = CriarOrdem();
        context.OrdensServico.Add(ordem);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var excecao = await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            CriarHandler(context).Handle(
                new TransicaoOrdemServicoCommand(ordem.Id, null),
                CancellationToken.None));

        Assert.Equal("Realize o check-in antes de iniciar a execução.", excecao.Message);
    }

    [Fact]
    public async Task ConfiguracaoObrigatoria_BloqueiaSemCheckIn_EPermiteAposRealizacao()
    {
        await using var context = Contexto();
        context.ConfiguracoesOperacionaisAtendimento.Add(new(
            _empresaId,
            NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Desabilitado,
            NivelExigenciaOperacional.Obrigatorio));
        var ordem = CriarOrdem();
        context.OrdensServico.Add(ordem);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var excecao = await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            CriarHandler(context).Handle(
                new TransicaoOrdemServicoCommand(ordem.Id, null),
                CancellationToken.None));
        Assert.Equal("Realize o check-in antes de iniciar a execução.", excecao.Message);

        var persistida = await context.OrdensServico
            .Include(item => item.Itens)
            .Include(item => item.Historico)
            .SingleAsync(item => item.Id == ordem.Id);
        persistida.RealizarCheckIn(
            new(
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Obrigatorio,
                null,
                []),
            null,
            null,
            _usuarioId);
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();

        var resultado = await CriarHandler(context).Handle(
            new TransicaoOrdemServicoCommand(ordem.Id, null),
            CancellationToken.None);

        Assert.Equal(StatusOrdemServico.EmExecucao, resultado.OrdemServico.Status);
        Assert.NotNull(resultado.OrdemServico.CheckInEmUtc);
    }

    private IniciarExecucaoHandler CriarHandler(DetaraDbContext context) => new(
        new UsuarioContextoTeste(_empresaId, _usuarioId),
        new OrdensServicoRepositorio(context),
        new PlataformaTeste(_empresaId),
        new ConfiguracoesOperacionaisRepositorio(context));

    private OrdemServico CriarOrdem() => new(
        _empresaId,
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
            _usuarioId,
            null)],
        _usuarioId,
        DateTime.UtcNow);

    private DetaraDbContext Contexto() => new(
        _options,
        new UsuarioContextoTeste(_empresaId, _usuarioId));

    private sealed class UsuarioContextoTeste(Guid empresaId, Guid usuarioId) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = usuarioId;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }

    private sealed class PlataformaTeste(Guid empresaId) : IPlataformaAtendimentoConsulta
    {
        public Task<EmpresaAtendimentoInterno?> ObterEmpresaAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult<EmpresaAtendimentoInterno?>(id == empresaId
                ? new(
                    empresaId,
                    "Empresa OS",
                    "Empresa OS Ltda.",
                    "11111111000111",
                    null,
                    null,
                    "America/Sao_Paulo")
                : null);

        public Task<IReadOnlyDictionary<Guid, string>> ObterNomesUsuariosAsync(
            Guid id,
            IReadOnlyCollection<Guid> usuarioIds,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<Guid, string>>(
                usuarioIds.ToDictionary(usuarioId => usuarioId, _ => "Operador"));
    }
}
