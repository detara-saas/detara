using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Autenticacao;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Preferencias;
using Detara.Infrastructure.Clientes;
using Detara.Infrastructure.Veiculos;
using Detara.Infrastructure.Catalogo;
using Detara.Application.Agenda;
using Detara.Infrastructure.Agenda;
using Detara.Infrastructure.Plataforma;
using Detara.Application.Atendimento;
using Detara.Infrastructure.Atendimento;
using Detara.Application.Clientes;
using Detara.Infrastructure.Storage;
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
        services.AddScoped<ICategoriasServicoRepositorio, CategoriasServicoRepositorio>();
        services.AddScoped<IServicosRepositorio, ServicosRepositorio>();
        services.AddScoped<IPacotesRepositorio, PacotesRepositorio>();
        services.AddScoped<IAgendaRepositorio, AgendaRepositorio>();
        services.AddScoped<IClientesAgendaConsulta, ClientesAgendaConsulta>();
        services.AddScoped<ICatalogoAgendaConsulta, CatalogoAgendaConsulta>();
        services.AddScoped<IFusoHorarioEmpresaConsulta, FusoHorarioEmpresaConsulta>();
        services.AddScoped<IOrcamentosRepositorio, OrcamentosRepositorio>();
        services.AddScoped<IClientesAtendimentoConsulta, ClientesAtendimentoConsulta>();
        services.AddScoped<ICatalogoAtendimentoConsulta, CatalogoAtendimentoConsulta>();
        services.AddScoped<IAgendaAtendimentoConsulta, AgendaAtendimentoConsulta>();
        services.AddScoped<IPlataformaAtendimentoConsulta, PlataformaAtendimentoConsulta>();
        services.AddScoped<IConfiguracoesOperacionaisRepositorio, ConfiguracoesOperacionaisRepositorio>();
        services.AddScoped<IVeiculoFotosRepositorio, VeiculoFotosRepositorio>();
        services.AddSingleton<IOrcamentoPdfGenerator, PdfOrcamentoGenerator>();
        var storageOptions = configuration.GetSection(StorageOptions.Secao).Get<StorageOptions>()
            ?? throw new InvalidOperationException("A configuração Storage deve ser informada.");
        if (!string.Equals(storageOptions.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"O provider de storage '{storageOptions.Provider}' não é suportado nesta versão.");
        }

        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Secao));
        services.AddSingleton<IArquivoStorage, LocalArquivoStorage>();
        services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

        return services;
    }
}
