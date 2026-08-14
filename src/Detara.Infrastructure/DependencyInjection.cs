using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Autenticacao;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Preferencias;
using Detara.Infrastructure.Clientes;
using Detara.Infrastructure.Veiculos;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "A connection string 'DefaultConnection' deve ser configurada.");

        services.AddDbContext<DetaraDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUsuarioAutenticacaoRepositorio, UsuarioAutenticacaoRepositorio>();
        services.AddScoped<ISenhaServico, SenhaServico>();
        services.AddScoped<IPreferenciasUsuarioRepositorio, PreferenciasUsuarioRepositorio>();
        services.AddScoped<IClientesRepositorio, ClientesRepositorio>();
        services.AddScoped<IVeiculosRepositorio, VeiculosRepositorio>();
        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

        return services;
    }
}
