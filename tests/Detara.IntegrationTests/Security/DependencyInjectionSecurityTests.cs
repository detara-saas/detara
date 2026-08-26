using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure;
using Detara.Infrastructure.Notificacoes;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Security;

public sealed class DependencyInjectionSecurityTests
{
    [Fact]
    public void GatewayWhatsAppHabilitado_RejeitaChaveFraca()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true",
                ["Storage:Provider"] = "Local",
                ["Storage:Local:RootPath"] = "data/test-security-storage",
                ["WhatsAppGateway:Enabled"] = "true",
                ["WhatsAppGateway:BaseUrl"] = "http://gateway.test:3000/",
                ["WhatsAppGateway:ApiKey"] = "curta",
                ["WhatsAppGateway:TimeoutSeconds"] = "30"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new ServiceCollection().AdicionarInfrastructure(configuracao));

        Assert.Contains("chave interna com pelo menos 32 caracteres",
            exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AliasesResend_AlimentamEmailOptions()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true",
                ["Storage:Provider"] = "Local",
                ["Storage:Local:RootPath"] = "data/test-security-storage",
                ["DETARA_RESEND_API_KEY"] = "segredo-local-de-teste",
                ["DETARA_EMAIL_FROM_ADDRESS"] = "onboarding@resend.dev"
            })
            .Build();
        var services = new ServiceCollection();

        services.AdicionarInfrastructure(configuracao);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<EmailOptions>>().Value;
        Assert.Equal("segredo-local-de-teste", options.ApiKey);
        Assert.Equal("onboarding@resend.dev", options.FromAddress);
    }

    [Fact]
    public void ServicoDeSenhaEHasher_CompartilhamLifetimeSingleton()
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true",
                ["Storage:Provider"] = "Local",
                ["Storage:Local:RootPath"] = "data/test-security-storage"
            })
            .Build();
        var services = new ServiceCollection();

        services.AdicionarInfrastructure(configuracao);

        Assert.Equal(
            ServiceLifetime.Singleton,
            services.Single(item => item.ServiceType == typeof(ISenhaServico)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Singleton,
            services.Single(item => item.ServiceType == typeof(IPasswordHasher<Usuario>)).Lifetime);
    }
}
