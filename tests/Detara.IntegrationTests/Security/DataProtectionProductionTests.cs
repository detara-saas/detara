using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.IntegrationTests.Security;

public sealed class DataProtectionProductionTests : IDisposable
{
    private readonly string _diretorio = Path.Combine(
        Path.GetTempPath(),
        "detara-dp-production-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void KeyRingProtegido_SobreviveARecriacaoDoProcesso()
    {
        Directory.CreateDirectory(_diretorio);
        using var certificate = CriarCertificado();
        const string texto = "convite-single-use-de-teste";

        string protegido;
        using (var primeiro = CriarProvider(certificate))
        {
            protegido = primeiro.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("Detara.Production.Readiness")
                .Protect(texto);
        }

        using (var segundo = CriarProvider(certificate))
        {
            var recuperado = segundo.GetRequiredService<IDataProtectionProvider>()
                .CreateProtector("Detara.Production.Readiness")
                .Unprotect(protegido);
            Assert.Equal(texto, recuperado);
        }

        var keyRing = string.Join(
            Environment.NewLine,
            Directory.GetFiles(_diretorio, "*.xml").Select(File.ReadAllText));
        Assert.Contains("encryptedSecret", keyRing, StringComparison.Ordinal);
        Assert.Contains("decryptorType", keyRing, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_diretorio))
        {
            Directory.Delete(_diretorio, true);
        }
    }

    private ServiceProvider CriarProvider(X509Certificate2 certificate)
    {
        var services = new ServiceCollection();
        services.AddDataProtection()
            .SetApplicationName("Detara.Platform")
            .PersistKeysToFileSystem(new DirectoryInfo(_diretorio))
            .ProtectKeysWithCertificate(certificate);
        return services.BuildServiceProvider();
    }

    private static X509Certificate2 CriarCertificado()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest(
            "CN=Detara Data Protection Tests",
            rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);
        return request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(1));
    }
}
