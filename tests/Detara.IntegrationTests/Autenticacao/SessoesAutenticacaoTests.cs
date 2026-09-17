using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Detara.Domain.Entidades;
using Detara.Domain.Identidade;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Autenticacao;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Autenticacao;

public sealed class SessoesAutenticacaoTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly RelogioTeste _relogio = new(new DateTimeOffset(2026, 9, 17, 12, 0, 0, TimeSpan.Zero));
    private DbContextOptions<DetaraDbContext> _dbOptions = null!;
    private Empresa _empresa = null!;
    private Usuario _usuario = null!;
    private AdministradorPlataforma _administrador = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _dbOptions = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using var db = CriarDbSistema();
        await db.Database.EnsureCreatedAsync();
        _empresa = new Empresa("Empresa Sessão", "Empresa Sessão Ltda", "12345678000190", "empresa-sessao");
        _administrador = new AdministradorPlataforma("Admin Global", "global@detara.test", "hash");
        _administrador.DefinirSegredoTotpProtegido("segredo-protegido");
        _administrador.AtivarMfa(1);
        db.AddRange(_empresa, _administrador);
        await db.SaveChangesAsync();
        var perfil = new Perfil(_empresa.Id, "Administrador");
        _usuario = new Usuario(_empresa.Id, perfil.Id, "Usuário Sessão", "sessao@detara.test", "hash");
        await using var tenant = new DetaraDbContext(
            _dbOptions,
            new ContextoTenant(_empresa.Id, _usuario.Id));
        tenant.AddRange(perfil, _usuario);
        await tenant.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task LoginSemRememberMe_CriaTokenOpacoHashEExpiracaoDeSessao()
    {
        await using var db = CriarDb();
        var servico = CriarServico(db);

        var token = await servico.CriarTenantAsync(
            _usuario.Id,
            _empresa.Id,
            false,
            CancellationToken.None);

        var persistida = await db.SessoesAutenticacao.SingleAsync();
        Assert.False(token.Persistente);
        Assert.False(persistida.Persistente);
        Assert.Equal(_relogio.GetUtcNow().UtcDateTime.AddHours(12), persistida.ExpiraEmUtc);
        Assert.NotEqual(token.Valor, persistida.TokenHash);
        Assert.DoesNotContain(token.Valor, db.ChangeTracker.DebugView.LongView, StringComparison.Ordinal);
        Assert.Equal(44, persistida.TokenHash.Length);
    }

    [Fact]
    public async Task LoginComRememberMe_CriaSessaoPersistentePorTrintaDias()
    {
        await using var db = CriarDb();
        var token = await CriarServico(db).CriarTenantAsync(
            _usuario.Id,
            _empresa.Id,
            true,
            CancellationToken.None);

        Assert.True(token.Persistente);
        Assert.Equal(_relogio.GetUtcNow().UtcDateTime.AddDays(30), token.ExpiraEmUtc);
        Assert.True((await db.SessoesAutenticacao.SingleAsync()).Persistente);
    }

    [Fact]
    public async Task Refresh_RotacionaTokenEPreservaClaimsConfiaveisDoTenant()
    {
        await using var db = CriarDb();
        var servico = CriarServico(db);
        var anterior = await servico.CriarTenantAsync(
            _usuario.Id,
            _empresa.Id,
            true,
            CancellationToken.None);

        var renovada = await servico.RenovarTenantAsync(anterior.Valor, CancellationToken.None);

        Assert.Equal(_usuario.Id, renovada.Candidato.Usuario.Id);
        Assert.Equal(_empresa.Id, renovada.Candidato.Empresa.Id);
        Assert.NotEqual(anterior.Valor, renovada.RefreshToken.Valor);
        var sessoes = await db.SessoesAutenticacao.OrderBy(x => x.CriadoEmUtc).ToArrayAsync();
        Assert.Equal(2, sessoes.Length);
        Assert.Equal("rotacionado", sessoes[0].MotivoRevogacao);
        Assert.Equal(sessoes[1].Id, sessoes[0].SubstituidoPorId);
        Assert.True(sessoes[1].EstaAtivaEm(_relogio.GetUtcNow().UtcDateTime));
        _ = await servico.RenovarTenantAsync(renovada.RefreshToken.Valor, CancellationToken.None);
    }

    [Fact]
    public async Task RefreshConcorrenteDentroDaJanela_NaoRevogaFamiliaValida()
    {
        await using var db = CriarDb();
        var servico = CriarServico(db);
        var anterior = await servico.CriarTenantAsync(
            _usuario.Id,
            _empresa.Id,
            false,
            CancellationToken.None);
        var renovada = await servico.RenovarTenantAsync(anterior.Valor, CancellationToken.None);

        await Assert.ThrowsAsync<SessaoAutenticacaoInvalidaException>(() =>
            servico.RenovarTenantAsync(anterior.Valor, CancellationToken.None));

        var atual = await db.SessoesAutenticacao.SingleAsync(x => x.RevogadoEmUtc == null);
        Assert.Null(atual.RevogadoEmUtc);
        _ = await servico.RenovarTenantAsync(renovada.RefreshToken.Valor, CancellationToken.None);
    }

    [Fact]
    public async Task RefreshExpirado_EhRejeitadoSemCriarNovaSessao()
    {
        await using var db = CriarDb();
        var servico = CriarServico(db);
        var token = await servico.CriarTenantAsync(
            _usuario.Id,
            _empresa.Id,
            false,
            CancellationToken.None);
        _relogio.Avancar(TimeSpan.FromHours(12).Add(TimeSpan.FromSeconds(1)));

        await Assert.ThrowsAsync<SessaoAutenticacaoInvalidaException>(() =>
            servico.RenovarTenantAsync(token.Valor, CancellationToken.None));

        Assert.Single(await db.SessoesAutenticacao.ToArrayAsync());
    }

    [Fact]
    public async Task ReplayForaDaJanela_RevogaTodaAFamilia()
    {
        await using var db = CriarDb();
        var servico = CriarServico(db);
        var anterior = await servico.CriarTenantAsync(
            _usuario.Id,
            _empresa.Id,
            false,
            CancellationToken.None);
        var renovada = await servico.RenovarTenantAsync(anterior.Valor, CancellationToken.None);
        _relogio.Avancar(TimeSpan.FromSeconds(31));

        await Assert.ThrowsAsync<SessaoAutenticacaoInvalidaException>(() =>
            servico.RenovarTenantAsync(anterior.Valor, CancellationToken.None));
        await Assert.ThrowsAsync<SessaoAutenticacaoInvalidaException>(() =>
            servico.RenovarTenantAsync(renovada.RefreshToken.Valor, CancellationToken.None));
        Assert.All(
            await db.SessoesAutenticacao.ToArrayAsync(),
            sessao => Assert.NotNull(sessao.RevogadoEmUtc));
    }

    [Fact]
    public async Task Logout_RevogaFamiliaEPermaneceIdempotente()
    {
        await using var db = CriarDb();
        var servico = CriarServico(db);
        var token = await servico.CriarTenantAsync(
            _usuario.Id,
            _empresa.Id,
            true,
            CancellationToken.None);

        await servico.RevogarTenantAsync(token.Valor, CancellationToken.None);
        await servico.RevogarTenantAsync(token.Valor, CancellationToken.None);

        Assert.Equal("logout", (await db.SessoesAutenticacao.SingleAsync()).MotivoRevogacao);
        await Assert.ThrowsAsync<SessaoAutenticacaoInvalidaException>(() =>
            servico.RenovarTenantAsync(token.Valor, CancellationToken.None));
    }

    [Fact]
    public async Task UsuarioDesativadoOuVersaoAlterada_NaoRenovaSessao()
    {
        await using var db = CriarDb();
        var servico = CriarServico(db);
        var token = await servico.CriarTenantAsync(
            _usuario.Id,
            _empresa.Id,
            false,
            CancellationToken.None);
        var usuario = await db.Usuarios.IgnoreQueryFilters().SingleAsync(x => x.Id == _usuario.Id);
        usuario.DesativarAcesso(usuario.Versao);
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<SessaoAutenticacaoInvalidaException>(() =>
            servico.RenovarTenantAsync(token.Valor, CancellationToken.None));
    }

    [Fact]
    public async Task Plataforma_SomenteCriaERenovaSessaoAposMfaValido()
    {
        await using var db = CriarDb();
        var servico = CriarServico(db);
        var token = await servico.CriarPlataformaAsync(_administrador.Id, CancellationToken.None);
        var renovada = await servico.RenovarPlataformaAsync(token.Valor, CancellationToken.None);

        Assert.Equal(_administrador.Id, renovada.AdministradorId);
        Assert.False(renovada.RefreshToken.Persistente);

        var admin = await db.AdministradoresPlataforma.SingleAsync(x => x.Id == _administrador.Id);
        admin.ResetarMfa();
        await db.SaveChangesAsync();
        await Assert.ThrowsAsync<SessaoAutenticacaoInvalidaException>(() =>
            servico.RenovarPlataformaAsync(renovada.RefreshToken.Valor, CancellationToken.None));
    }

    private SessoesAutenticacaoServico CriarServico(DetaraDbContext db) => new(
        db,
        new UsuarioAutenticacaoRepositorio(db),
        Options.Create(new SessaoAutenticacaoOptions()),
        _relogio,
        NullLogger<SessoesAutenticacaoServico>.Instance);

    private DetaraDbContext CriarDb() => new(
        _dbOptions,
        new ContextoTenant(_empresa.Id, _usuario.Id));

    private DetaraDbContext CriarDbSistema() => new(_dbOptions, ContextoAnonimo.Instancia);

    private sealed class RelogioTeste(DateTimeOffset agora) : TimeProvider
    {
        private DateTimeOffset _agora = agora;
        public override DateTimeOffset GetUtcNow() => _agora;
        public void Avancar(TimeSpan tempo) => _agora = _agora.Add(tempo);
    }

    private sealed class ContextoAnonimo : IUsuarioContexto
    {
        public static ContextoAnonimo Instancia { get; } = new();
        public Guid UsuarioId => Guid.Empty;
        public Guid EmpresaId => Guid.Empty;
        public bool EstaAutenticado => false;
    }

    private sealed class ContextoTenant(Guid empresaId, Guid usuarioId) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = usuarioId;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }
}
