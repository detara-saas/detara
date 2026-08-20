using Detara.Application.Abstracoes;
using Detara.Application.Plataforma;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using OtpNet;

namespace Detara.IntegrationTests.Plataforma;

public sealed class AutenticacaoPlataformaTests : IAsyncLifetime
{
    private const string Senha = "uma-passphrase-segura";
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly IPasswordHasher<AdministradorPlataforma> _hasher =
        new PasswordHasher<AdministradorPlataforma>();
    private DbContextOptions<DetaraDbContext> _options = null!;
    private AdministradorPlataforma _administrador = null!;
    private string _dataProtectionPath = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlite(_connection)
            .Options;
        _dataProtectionPath = Path.Combine(
            Path.GetTempPath(),
            $"detara-platform-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dataProtectionPath);

        await using var db = CriarContexto();
        await db.Database.EnsureCreatedAsync();
        _administrador = new AdministradorPlataforma(
            "Admin Plataforma",
            "admin@detara.local",
            "pendente");
        _administrador.AlterarSenhaHash(_hasher.HashPassword(_administrador, Senha));
        db.AdministradoresPlataforma.Add(_administrador);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        await _connection.DisposeAsync();
        if (Directory.Exists(_dataProtectionPath))
        {
            Directory.Delete(_dataProtectionPath, recursive: true);
        }
    }

    [Fact]
    public async Task PrimeiroAcesso_ExigeEnrollmentERetornaRecoveryCodesUmaVez()
    {
        await using var db = CriarContexto();
        var servico = CriarServico(db);

        var inicio = await servico.IniciarAsync(_administrador.Email, Senha, CancellationToken.None);
        Assert.False(inicio.MfaConfigurado);

        var configuracao = await servico.ObterConfiguracaoMfaAsync(
            inicio.Desafio,
            CancellationToken.None);
        Assert.StartsWith("otpauth://totp/", configuracao.OtpAuthUri, StringComparison.Ordinal);
        Assert.StartsWith("data:image/svg+xml;base64,", configuracao.QrCodeSvgDataUrl, StringComparison.Ordinal);
        var codigo = GerarTotp(configuracao.ChaveManual);
        var resultado = await servico.AtivarMfaAsync(
            inicio.Desafio,
            codigo,
            "trace-enrollment",
            CancellationToken.None);

        Assert.Equal(10, resultado.CodigosRecuperacao.Count);
        Assert.Equal(10, resultado.CodigosRecuperacao.Distinct().Count());
        Assert.All(resultado.CodigosRecuperacao, item => Assert.DoesNotContain(item, " "));
        Assert.Equal(
            10,
            await db.CodigosRecuperacaoAdministradoresPlataforma.CountAsync());
        Assert.DoesNotContain(
            resultado.CodigosRecuperacao.First(),
            (await db.CodigosRecuperacaoAdministradoresPlataforma.FirstAsync()).CodigoHash,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Totp_ReplayERecusadoERecoveryCodeEConsumidoUmaUnicaVez()
    {
        var codigos = await AtivarMfaAsync();

        await using (var db = CriarContexto())
        {
            var servico = CriarServico(db);
            var inicio = await servico.IniciarAsync(_administrador.Email, Senha, CancellationToken.None);
            var configuracao = await ObterSegredoProtegidoAsync(db);
            var codigoRepetido = GerarTotp(configuracao);

            await Assert.ThrowsAsync<CodigoMfaInvalidoException>(() => servico.VerificarMfaAsync(
                inicio.Desafio,
                codigoRepetido,
                "trace-replay",
                CancellationToken.None));
        }

        await using (var db = CriarContexto())
        {
            var servico = CriarServico(db);
            var inicio = await servico.IniciarAsync(_administrador.Email, Senha, CancellationToken.None);
            var autenticacao = await servico.VerificarMfaAsync(
                inicio.Desafio,
                codigos.First(),
                "trace-recovery",
                CancellationToken.None);
            Assert.Equal(_administrador.Id, autenticacao.Identidade.Id);
        }

        await using (var db = CriarContexto())
        {
            var servico = CriarServico(db);
            var inicio = await servico.IniciarAsync(_administrador.Email, Senha, CancellationToken.None);
            await Assert.ThrowsAsync<CodigoMfaInvalidoException>(() => servico.VerificarMfaAsync(
                inicio.Desafio,
                codigos.First(),
                "trace-recovery-replay",
                CancellationToken.None));
        }
    }

    [Theory]
    [InlineData("admin-inexistente@detara.local", Senha)]
    [InlineData("admin@detara.local", "senha-incorreta")]
    public async Task CredenciaisInvalidas_UsamErroGenerico(string email, string senha)
    {
        await using var db = CriarContexto();
        var servico = CriarServico(db);

        var erro = await Assert.ThrowsAsync<CredenciaisPlataformaInvalidasException>(() =>
            servico.IniciarAsync(email, senha, CancellationToken.None));

        Assert.Equal("Não foi possível autenticar com as credenciais informadas.", erro.Message);
    }

    [Fact]
    public async Task DesafioMfa_EBloqueadoAposCincoCodigosInvalidos()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        await using var db = CriarContexto();
        var servico = CriarServico(db, cache);
        var inicio = await servico.IniciarAsync(_administrador.Email, Senha, CancellationToken.None);
        var configuracao = await servico.ObterConfiguracaoMfaAsync(inicio.Desafio, CancellationToken.None);

        for (var tentativa = 0; tentativa < 5; tentativa++)
        {
            await Assert.ThrowsAsync<CodigoMfaInvalidoException>(() => servico.AtivarMfaAsync(
                inicio.Desafio,
                "codigo-invalido",
                null,
                CancellationToken.None));
        }

        await Assert.ThrowsAsync<CodigoMfaInvalidoException>(() => servico.AtivarMfaAsync(
            inicio.Desafio,
            GerarTotp(configuracao.ChaveManual),
            null,
            CancellationToken.None));
    }

    [Fact]
    public async Task AlteracaoSenhaOuDesativacao_RevogaSessaoPlataforma()
    {
        var codigos = await AtivarMfaAsync();
        _ = codigos;
        long versao;
        await using (var db = CriarContexto())
        {
            var admin = await db.AdministradoresPlataforma.SingleAsync();
            versao = admin.VersaoSeguranca;
            Assert.True(await CriarServico(db).RevalidarAsync(admin.Id, versao, CancellationToken.None));
            admin.AlterarSenhaHash(_hasher.HashPassword(admin, "outra-passphrase-segura"));
            await db.SaveChangesAsync();
        }

        await using (var db = CriarContexto())
        {
            Assert.False(await CriarServico(db).RevalidarAsync(_administrador.Id, versao, CancellationToken.None));
            var admin = await db.AdministradoresPlataforma.SingleAsync();
            admin.DesativarComRevogacao();
            await db.SaveChangesAsync();
        }

        await using var verificacao = CriarContexto();
        var atual = await verificacao.AdministradoresPlataforma.SingleAsync();
        Assert.False(await CriarServico(verificacao).RevalidarAsync(
            atual.Id,
            atual.VersaoSeguranca,
            CancellationToken.None));
    }

    private async Task<IReadOnlyCollection<string>> AtivarMfaAsync()
    {
        await using var db = CriarContexto();
        var servico = CriarServico(db);
        var inicio = await servico.IniciarAsync(_administrador.Email, Senha, CancellationToken.None);
        var configuracao = await servico.ObterConfiguracaoMfaAsync(inicio.Desafio, CancellationToken.None);
        return (await servico.AtivarMfaAsync(
            inicio.Desafio,
            GerarTotp(configuracao.ChaveManual),
            "trace",
            CancellationToken.None)).CodigosRecuperacao;
    }

    private async Task<string> ObterSegredoProtegidoAsync(DetaraDbContext db)
    {
        var administrador = await db.AdministradoresPlataforma.SingleAsync();
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(_dataProtectionPath),
            configuracao => configuracao.SetApplicationName("Detara.Platform.Tests"));
        return provider
            .CreateProtector("Detara.Platform.TotpSecret.v1")
            .Unprotect(administrador.SegredoTotpProtegido!);
    }

    private IAutenticacaoPlataformaServico CriarServico(
        DetaraDbContext db,
        IMemoryCache? cache = null)
    {
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(_dataProtectionPath),
            configuracao => configuracao.SetApplicationName("Detara.Platform.Tests"));
        return new AutenticacaoPlataformaServico(
            db,
            _hasher,
            provider,
            Options.Create(new PlataformaOptions()),
            cache ?? new MemoryCache(new MemoryCacheOptions()),
            NullLogger<AutenticacaoPlataformaServico>.Instance);
    }

    private static string GerarTotp(string segredo) => new Totp(
        Base32Encoding.ToBytes(segredo),
        step: 30,
        mode: OtpHashMode.Sha1,
        totpSize: 6).ComputeTotp();

    private DetaraDbContext CriarContexto() => new(_options, ContextoAnonimo.Instancia);

    private sealed class ContextoAnonimo : IUsuarioContexto
    {
        public static ContextoAnonimo Instancia { get; } = new();
        public Guid UsuarioId => Guid.Empty;
        public Guid EmpresaId => Guid.Empty;
        public bool EstaAutenticado => false;
    }
}
