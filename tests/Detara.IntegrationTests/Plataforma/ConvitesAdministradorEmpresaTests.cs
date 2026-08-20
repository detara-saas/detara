using Detara.Application.Abstracoes;
using Detara.Application.Plataforma;
using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Plataforma;

public sealed class ConvitesAdministradorEmpresaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly PasswordHasher<Usuario> _hasher = new();
    private DbContextOptions<DetaraDbContext> _options = null!;
    private AdministradorPlataforma _administrador = null!;
    private Guid _empresaId;
    private Guid _usuarioId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        await using var db = CriarContexto();
        await db.Database.EnsureCreatedAsync();
        _administrador = new AdministradorPlataforma("Admin", "admin@detara.local", "hash");
        _administrador.DefinirSegredoTotpProtegido("protegido");
        _administrador.AtivarMfa(1);
        db.AdministradoresPlataforma.Add(_administrador);
        await db.SaveChangesAsync();

        var plataforma = new AdministracaoPlataformaServico(db, _options, _hasher);
        var empresa = await plataforma.ProvisionarEmpresaAsync(
            _administrador.Id,
            new ProvisionarEmpresaEntrada(
                "Empresa Convite",
                "Empresa Convite Ltda",
                "12345678000190",
                null,
                null,
                "America/Sao_Paulo",
                "Admin Tenant",
                "tenant-admin@empresa.local"),
            "trace",
            CancellationToken.None);
        _empresaId = empresa.Id;
        _usuarioId = empresa.AdministradorUsuarioId;
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task ConviteValido_AtivaUsuarioDefineSenhaEAceitaUmaUnicaVez()
    {
        const string token = "token-seguro-de-alta-entropia-simulado";
        await MarcarEnviadoAsync(token, TimeSpan.FromHours(2));
        await using var db = CriarContexto();
        var servico = CriarServico(db);

        var validado = await servico.ValidarAsync(token, CancellationToken.None);
        Assert.Equal("Empresa Convite", validado.EmpresaNome);
        Assert.Equal("te***@empresa.local", validado.EmailMascarado);

        await servico.AceitarAsync(
            token,
            "passphrase-escolhida-pelo-tenant",
            "trace-aceite",
            CancellationToken.None);

        await using var verificacao = CriarContexto();
        var usuario = await verificacao.Usuarios.IgnoreQueryFilters().SingleAsync(x => x.Id == _usuarioId);
        var convite = await verificacao.ConvitesAdministradoresEmpresa.SingleAsync();
        Assert.True(usuario.EhAtivo);
        Assert.Equal(
            PasswordVerificationResult.Success,
            _hasher.VerifyHashedPassword(usuario, usuario.SenhaHash, "passphrase-escolhida-pelo-tenant"));
        Assert.Equal(StatusConviteAdministradorEmpresa.Aceito, convite.Status);
        Assert.Null(convite.TokenHash);
        Assert.Contains(
            await verificacao.AuditoriasPlataforma.ToListAsync(),
            x => x.TipoAcao == AcoesAuditoriaPlataforma.ConviteAceito && x.TraceId == "trace-aceite");

        await Assert.ThrowsAsync<ConviteAdministradorInvalidoException>(() =>
            CriarServico(verificacao).AceitarAsync(token, "outra-passphrase-segura", null, CancellationToken.None));
    }

    [Fact]
    public async Task ConviteExpirado_ERecusadoComErroGenerico()
    {
        const string token = "token-que-expira";
        await MarcarEnviadoAsync(token, TimeSpan.FromMilliseconds(5));
        await Task.Delay(30);

        await using var db = CriarContexto();
        var erro = await Assert.ThrowsAsync<ConviteAdministradorInvalidoException>(() =>
            CriarServico(db).ValidarAsync(token, CancellationToken.None));

        Assert.Equal("O convite é inválido, expirou ou já foi utilizado.", erro.Message);
    }

    [Fact]
    public async Task Reenvio_InvalidaTokenAntigoENovoTokenPermaneceValido()
    {
        const string tokenAntigo = "token-antigo";
        const string tokenNovo = "token-novo";
        await MarcarEnviadoAsync(tokenAntigo, TimeSpan.FromHours(2));
        await using (var db = CriarContexto())
        {
            var plataforma = new AdministracaoPlataformaServico(db, _options, _hasher);
            await plataforma.ReenviarConviteAsync(_administrador.Id, _empresaId, "trace-reenvio", CancellationToken.None);
        }

        await using (var db = CriarContexto())
        {
            await Assert.ThrowsAsync<ConviteAdministradorInvalidoException>(() =>
                CriarServico(db).ValidarAsync(tokenAntigo, CancellationToken.None));
            var convite = await db.ConvitesAdministradoresEmpresa.SingleAsync();
            var envioEm = convite.ProximaTentativaEnvioEmUtc!.Value.AddMilliseconds(1);
            convite.IniciarEnvio(
                ConvitesAdministradoresEmpresaServico.HashToken(tokenNovo),
                envioEm.AddHours(2),
                envioEm);
            convite.RegistrarEnvio("provider-novo", envioEm);
            await db.SaveChangesAsync();
        }

        await using var verificacao = CriarContexto();
        Assert.Equal(
            "Empresa Convite",
            (await CriarServico(verificacao).ValidarAsync(tokenNovo, CancellationToken.None)).EmpresaNome);
    }

    [Fact]
    public async Task EmpresaSuspensaOuUsuarioJaAtivo_ImpedemAceite()
    {
        const string token = "token-estado-invalido";
        await MarcarEnviadoAsync(token, TimeSpan.FromHours(2));
        await using (var db = CriarContexto())
        {
            var empresa = await db.Empresas.SingleAsync(x => x.Id == _empresaId);
            empresa.Suspender();
            await db.SaveChangesAsync();
        }

        await using (var db = CriarContexto())
        {
            await Assert.ThrowsAsync<ConviteAdministradorInvalidoException>(() =>
                CriarServico(db).AceitarAsync(token, "passphrase-segura", null, CancellationToken.None));
            var empresa = await db.Empresas.SingleAsync(x => x.Id == _empresaId);
            empresa.Reativar();
            await db.SaveChangesAsync();
        }

        await using (var db = CriarContextoTenant())
        {
            var usuario = await db.Usuarios.IgnoreQueryFilters().SingleAsync(x => x.Id == _usuarioId);
            usuario.Ativar();
            await db.SaveChangesAsync();
        }

        await using var verificacao = CriarContexto();
        await Assert.ThrowsAsync<ConviteAdministradorInvalidoException>(() =>
            CriarServico(verificacao).AceitarAsync(token, "passphrase-segura", null, CancellationToken.None));
    }

    private async Task MarcarEnviadoAsync(string token, TimeSpan validade)
    {
        await using var db = CriarContexto();
        var convite = await db.ConvitesAdministradoresEmpresa.SingleAsync();
        var envioEm = convite.ProximaTentativaEnvioEmUtc!.Value.AddMilliseconds(1);
        convite.IniciarEnvio(
            ConvitesAdministradoresEmpresaServico.HashToken(token),
            envioEm.Add(validade),
            envioEm);
        convite.RegistrarEnvio("provider", envioEm);
        await db.SaveChangesAsync();
    }

    private IConvitesAdministradoresEmpresaServico CriarServico(DetaraDbContext db) =>
        new ConvitesAdministradoresEmpresaServico(db, _options, _hasher);

    private DetaraDbContext CriarContexto() => new(_options, ContextoAnonimo.Instancia);
    private DetaraDbContext CriarContextoTenant() => new(_options, new ContextoTenant(_empresaId));
    private sealed class ContextoAnonimo : IUsuarioContexto
    {
        public static ContextoAnonimo Instancia { get; } = new();
        public Guid UsuarioId => Guid.Empty;
        public Guid EmpresaId => Guid.Empty;
        public bool EstaAutenticado => false;
    }

    private sealed class ContextoTenant(Guid empresaId) : IUsuarioContexto
    {
        public Guid UsuarioId => Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }
}
