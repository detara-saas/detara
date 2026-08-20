using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Detara.Application.Plataforma;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OtpNet;
using QRCoder;

namespace Detara.Infrastructure.Plataforma;

internal sealed class AutenticacaoPlataformaServico : IAutenticacaoPlataformaServico
{
    private const string PurposeDesafio = "Detara.Platform.MfaChallenge.v1";
    private const string PurposeSegredo = "Detara.Platform.TotpSecret.v1";
    private const string TipoEnrollment = "enrollment";
    private const string TipoLogin = "login";
    private const int QuantidadeCodigosRecuperacao = 10;
    private readonly DetaraDbContext _db;
    private readonly IPasswordHasher<AdministradorPlataforma> _passwordHasher;
    private readonly ITimeLimitedDataProtector _protetorDesafio;
    private readonly IDataProtector _protetorSegredo;
    private readonly PlataformaOptions _options;
    private readonly AdministradorPlataforma _administradorFicticio;
    private readonly string _hashFicticio;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AutenticacaoPlataformaServico> _logger;

    public AutenticacaoPlataformaServico(
        DetaraDbContext db,
        IPasswordHasher<AdministradorPlataforma> passwordHasher,
        IDataProtectionProvider dataProtectionProvider,
        IOptions<PlataformaOptions> options,
        IMemoryCache cache,
        ILogger<AutenticacaoPlataformaServico> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _protetorDesafio = dataProtectionProvider
            .CreateProtector(PurposeDesafio)
            .ToTimeLimitedDataProtector();
        _protetorSegredo = dataProtectionProvider.CreateProtector(PurposeSegredo);
        _options = options.Value;
        _cache = cache;
        _logger = logger;
        _administradorFicticio = new AdministradorPlataforma(
            "Administrador não encontrado",
            "nao-encontrado@invalid.local",
            "temporario");
        _hashFicticio = passwordHasher.HashPassword(
            _administradorFicticio,
            Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }

    public async Task<InicioAutenticacaoPlataformaResultado> IniciarAsync(
        string email,
        string senha,
        CancellationToken cancellationToken)
    {
        var normalizado = email.Trim().ToUpperInvariant();
        var administrador = await _db.AdministradoresPlataforma
            .SingleOrDefaultAsync(x => x.EmailNormalizado == normalizado, cancellationToken);
        if (administrador is null)
        {
            _ = _passwordHasher.VerifyHashedPassword(_administradorFicticio, _hashFicticio, senha);
            _logger.LogWarning("Falha de autenticação de Platform Admin.");
            throw new CredenciaisPlataformaInvalidasException();
        }

        var senhaValida = _passwordHasher.VerifyHashedPassword(
            administrador,
            administrador.SenhaHash,
            senha) is not PasswordVerificationResult.Failed;
        if (!senhaValida || !administrador.EhAtivo)
        {
            _logger.LogWarning("Falha de autenticação de Platform Admin.");
            throw new CredenciaisPlataformaInvalidasException();
        }

        var minutos = Math.Clamp(_options.DesafioMfaExpiracaoMinutos, 3, 10);
        var expiraEm = DateTime.UtcNow.AddMinutes(minutos);
        var payload = new DesafioMfaPayload(
            administrador.Id,
            administrador.VersaoSeguranca,
            administrador.MfaHabilitado ? TipoLogin : TipoEnrollment);
        var desafio = _protetorDesafio.Protect(
            JsonSerializer.Serialize(payload),
            TimeSpan.FromMinutes(minutos));
        return new(desafio, expiraEm, administrador.MfaHabilitado);
    }

    public async Task<ConfiguracaoMfaPlataformaResultado> ObterConfiguracaoMfaAsync(
        string desafio,
        CancellationToken cancellationToken)
    {
        var (payload, administrador) = await ValidarDesafioAsync(
            desafio,
            TipoEnrollment,
            cancellationToken);
        if (administrador.MfaHabilitado)
        {
            throw new CodigoMfaInvalidoException();
        }

        string chaveManual;
        if (string.IsNullOrWhiteSpace(administrador.SegredoTotpProtegido))
        {
            chaveManual = Base32Encoding.ToString(KeyGeneration.GenerateRandomKey(20));
            administrador.DefinirSegredoTotpProtegido(_protetorSegredo.Protect(chaveManual));
            await _db.SaveChangesAsync(cancellationToken);
        }
        else
        {
            chaveManual = DesprotegerSegredo(administrador.SegredoTotpProtegido);
        }

        var otpAuthUri = new OtpUri(
            OtpType.Totp,
            chaveManual,
            administrador.Email,
            "Detara").ToString();
        using var qrData = QRCodeGenerator.GenerateQrCode(otpAuthUri, QRCodeGenerator.ECCLevel.M);
        var svg = new SvgQRCode(qrData).GetGraphic(6);
        var dataUrl = "data:image/svg+xml;base64," +
            Convert.ToBase64String(Encoding.UTF8.GetBytes(svg));
        _ = payload;
        return new(chaveManual, otpAuthUri, dataUrl);
    }

    public async Task<AutenticacaoMfaPlataformaResultado> AtivarMfaAsync(
        string desafio,
        string codigo,
        string? traceId,
        CancellationToken cancellationToken)
    {
        ValidarLimiteTentativas(desafio);
        var (_, administrador) = await ValidarDesafioAsync(
            desafio,
            TipoEnrollment,
            cancellationToken);
        if (administrador.MfaHabilitado ||
            string.IsNullOrWhiteSpace(administrador.SegredoTotpProtegido))
        {
            throw new CodigoMfaInvalidoException();
        }

        long timestep;
        try
        {
            timestep = VerificarTotp(administrador, codigo);
        }
        catch (CodigoMfaInvalidoException)
        {
            RegistrarFalhaDesafio(desafio);
            throw;
        }

        administrador.AtivarMfa(timestep);
        administrador.RegistrarLogin(DateTime.UtcNow);
        var codigos = SubstituirCodigosRecuperacao(administrador.Id);
        _db.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
            administrador.Id,
            AcoesAuditoriaPlataforma.MfaConfigurado,
            null,
            administrador.Id,
            traceId,
            "MFA obrigatório configurado."));
        await _db.SaveChangesAsync(cancellationToken);
        RemoverTentativas(desafio);
        return new(MapearIdentidade(administrador), codigos);
    }

    public async Task<AutenticacaoMfaPlataformaResultado> VerificarMfaAsync(
        string desafio,
        string codigo,
        string? traceId,
        CancellationToken cancellationToken)
    {
        ValidarLimiteTentativas(desafio);
        var (_, administrador) = await ValidarDesafioAsync(
            desafio,
            TipoLogin,
            cancellationToken);
        if (!administrador.MfaHabilitado ||
            string.IsNullOrWhiteSpace(administrador.SegredoTotpProtegido))
        {
            throw new CodigoMfaInvalidoException();
        }

        if (EhCodigoTotp(codigo))
        {
            try
            {
                administrador.RegistrarTimestepTotp(VerificarTotp(administrador, codigo));
            }
            catch (Exception exception) when (
                exception is CodigoMfaInvalidoException or InvalidOperationException)
            {
                RegistrarFalhaDesafio(desafio);
                throw new CodigoMfaInvalidoException();
            }
        }
        else if (!await ConsumirCodigoRecuperacaoAsync(administrador.Id, codigo, cancellationToken))
        {
            RegistrarFalhaDesafio(desafio);
            throw new CodigoMfaInvalidoException();
        }

        administrador.RegistrarLogin(DateTime.UtcNow);
        _db.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
            administrador.Id,
            AcoesAuditoriaPlataforma.LoginRealizado,
            null,
            administrador.Id,
            traceId,
            "Login administrativo com MFA concluído."));
        await _db.SaveChangesAsync(cancellationToken);
        RemoverTentativas(desafio);
        return new(MapearIdentidade(administrador), Array.Empty<string>());
    }

    public async Task<IReadOnlyCollection<string>> RegenerarCodigosRecuperacaoAsync(
        Guid administradorPlataformaId,
        string senhaAtual,
        string codigoTotp,
        string? traceId,
        CancellationToken cancellationToken)
    {
        var administrador = await _db.AdministradoresPlataforma
            .SingleOrDefaultAsync(x => x.Id == administradorPlataformaId, cancellationToken)
            ?? throw new CredenciaisPlataformaInvalidasException();
        var senhaValida = _passwordHasher.VerifyHashedPassword(
            administrador,
            administrador.SenhaHash,
            senhaAtual) is not PasswordVerificationResult.Failed;
        if (!senhaValida || !administrador.EhAtivo || !administrador.MfaHabilitado)
        {
            throw new CredenciaisPlataformaInvalidasException();
        }

        administrador.RegistrarTimestepTotp(VerificarTotp(administrador, codigoTotp));
        var codigos = SubstituirCodigosRecuperacao(administrador.Id);
        _db.AuditoriasPlataforma.Add(new AuditoriaPlataforma(
            administrador.Id,
            AcoesAuditoriaPlataforma.RecoveryCodesRegenerados,
            null,
            administrador.Id,
            traceId,
            "Códigos de recuperação regenerados; anteriores invalidados."));
        await _db.SaveChangesAsync(cancellationToken);
        return codigos;
    }

    public Task<bool> RevalidarAsync(
        Guid administradorPlataformaId,
        long versaoSeguranca,
        CancellationToken cancellationToken) =>
        _db.AdministradoresPlataforma.AsNoTracking().AnyAsync(
            x => x.Id == administradorPlataformaId &&
                x.EhAtivo &&
                x.MfaHabilitado &&
                x.VersaoSeguranca == versaoSeguranca,
            cancellationToken);

    private async Task<(DesafioMfaPayload Payload, AdministradorPlataforma Administrador)> ValidarDesafioAsync(
        string desafio,
        string tipoEsperado,
        CancellationToken cancellationToken)
    {
        try
        {
            var json = _protetorDesafio.Unprotect(desafio, out _);
            var payload = JsonSerializer.Deserialize<DesafioMfaPayload>(json)
                ?? throw new CodigoMfaInvalidoException();
            if (!string.Equals(payload.Tipo, tipoEsperado, StringComparison.Ordinal))
            {
                throw new CodigoMfaInvalidoException();
            }

            var administrador = await _db.AdministradoresPlataforma.SingleOrDefaultAsync(
                x => x.Id == payload.AdministradorId,
                cancellationToken);
            if (administrador is null ||
                !administrador.EhAtivo ||
                administrador.VersaoSeguranca != payload.VersaoSeguranca)
            {
                throw new CodigoMfaInvalidoException();
            }

            return (payload, administrador);
        }
        catch (Exception exception) when (exception is CryptographicException or JsonException or FormatException)
        {
            throw new CodigoMfaInvalidoException();
        }
    }

    private long VerificarTotp(AdministradorPlataforma administrador, string codigo)
    {
        try
        {
            var chave = Base32Encoding.ToBytes(DesprotegerSegredo(administrador.SegredoTotpProtegido!));
            var totp = new Totp(chave, step: 30, mode: OtpHashMode.Sha1, totpSize: 6);
            if (!totp.VerifyTotp(
                    DateTime.UtcNow,
                    codigo.Trim(),
                    out var timestep,
                    new VerificationWindow(previous: 1, future: 1)) ||
                administrador.UltimoTimestepTotpAceito is not null &&
                timestep <= administrador.UltimoTimestepTotpAceito)
            {
                throw new CodigoMfaInvalidoException();
            }

            return timestep;
        }
        catch (CryptographicException)
        {
            throw new CodigoMfaInvalidoException();
        }
    }

    private string DesprotegerSegredo(string segredoProtegido) => _protetorSegredo.Unprotect(segredoProtegido);

    private IReadOnlyCollection<string> SubstituirCodigosRecuperacao(Guid administradorId)
    {
        var anteriores = _db.CodigosRecuperacaoAdministradoresPlataforma
            .Where(x => x.AdministradorPlataformaId == administradorId);
        _db.CodigosRecuperacaoAdministradoresPlataforma.RemoveRange(anteriores);
        var codigos = Enumerable.Range(0, QuantidadeCodigosRecuperacao)
            .Select(_ => GerarCodigoRecuperacao())
            .ToArray();
        _db.CodigosRecuperacaoAdministradoresPlataforma.AddRange(codigos.Select(codigo =>
            new CodigoRecuperacaoAdministradorPlataforma(administradorId, HashCodigo(codigo))));
        return codigos;
    }

    private async Task<bool> ConsumirCodigoRecuperacaoAsync(
        Guid administradorId,
        string codigo,
        CancellationToken cancellationToken)
    {
        var hash = HashCodigo(codigo);
        var candidatos = await _db.CodigosRecuperacaoAdministradoresPlataforma
            .Where(x => x.AdministradorPlataformaId == administradorId && x.UtilizadoEmUtc == null)
            .ToArrayAsync(cancellationToken);
        var esperado = Convert.FromBase64String(hash);
        var encontrado = candidatos.FirstOrDefault(x =>
            CryptographicOperations.FixedTimeEquals(
                Convert.FromBase64String(x.CodigoHash),
                esperado));
        if (encontrado is null)
        {
            return false;
        }

        encontrado.MarcarUtilizado(DateTime.UtcNow);
        return true;
    }

    private static string GerarCodigoRecuperacao()
    {
        var valor = Base32Encoding.ToString(RandomNumberGenerator.GetBytes(10));
        return $"{valor[..4]}-{valor[4..8]}-{valor[8..12]}-{valor[12..16]}";
    }

    internal static string HashCodigo(string codigo)
    {
        var normalizado = codigo.Replace("-", string.Empty, StringComparison.Ordinal)
            .Trim()
            .ToUpperInvariant();
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(normalizado)));
    }

    private static bool EhCodigoTotp(string codigo) =>
        codigo.Length == 6 && codigo.All(char.IsAsciiDigit);

    private void ValidarLimiteTentativas(string desafio)
    {
        if (_cache.TryGetValue<int>(ChaveTentativas(desafio), out var tentativas) && tentativas >= 5)
        {
            throw new CodigoMfaInvalidoException();
        }
    }

    private void RegistrarFalhaDesafio(string desafio)
    {
        var chave = ChaveTentativas(desafio);
        _cache.TryGetValue<int>(chave, out var tentativas);
        _cache.Set(chave, tentativas + 1, TimeSpan.FromMinutes(10));
    }

    private void RemoverTentativas(string desafio) => _cache.Remove(ChaveTentativas(desafio));

    private static string ChaveTentativas(string desafio) =>
        "platform-mfa-attempts:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(desafio)));

    private static IdentidadeAdministradorPlataformaResultado MapearIdentidade(
        AdministradorPlataforma administrador) => new(
            administrador.Id,
            administrador.Nome,
            administrador.Email,
            administrador.VersaoSeguranca);

    private sealed record DesafioMfaPayload(
        Guid AdministradorId,
        long VersaoSeguranca,
        string Tipo);
}
