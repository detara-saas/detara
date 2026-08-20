using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.IntegrationTests.Security;

public sealed class DependencyInjectionSecurityTests
{
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
