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
using Detara.Application.Financeiro;
using Detara.Infrastructure.Financeiro;
using Detara.Application.Notificacoes;
using Detara.Infrastructure.Notificacoes;
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
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A connection string 'DefaultConnection' deve ser configurada por secret ou variável de ambiente.");
        }

        services.AddDbContext<DetaraDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IUsuarioAutenticacaoRepositorio, UsuarioAutenticacaoRepositorio>();
        services.AddSingleton<ISenhaServico, SenhaServico>();
        services.AddScoped<IValidadorIdentidadeAutenticada, ValidadorIdentidadeAutenticada>();
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
        services.AddScoped<IOrdensServicoRepositorio, OrdensServicoRepositorio>();
        services.AddScoped<IClientesAtendimentoConsulta, ClientesAtendimentoConsulta>();
        services.AddScoped<ICatalogoAtendimentoConsulta, CatalogoAtendimentoConsulta>();
        services.AddScoped<IAgendaAtendimentoConsulta, AgendaAtendimentoConsulta>();
        services.AddScoped<IPlataformaAtendimentoConsulta, PlataformaAtendimentoConsulta>();
        services.AddScoped<IConfiguracoesOperacionaisRepositorio, ConfiguracoesOperacionaisRepositorio>();
        services.AddScoped<IFinanceiroRepositorio, FinanceiroRepositorio>();
        services.AddScoped<IPlataformaFinanceiroConsulta, PlataformaFinanceiroConsulta>();
        services.AddScoped<INotificacoesRepositorio, NotificacoesRepositorio>();
        services.AddScoped<IPlataformaNotificacoesConsulta, PlataformaNotificacoesConsulta>();
        services.AddScoped<IClientesNotificacoesConsulta, ClientesNotificacoesConsulta>();
        services.AddSingleton<IRenderizadorTemplateEmail, RenderizadorTemplateEmail>();
        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.Secao));
        services.Configure<FilaNotificacoesOptions>(configuration.GetSection(FilaNotificacoesOptions.Secao));
        services.AddHttpClient<IProvedorEmail, ResendEmailProvider>(client =>
        {
            client.BaseAddress = new Uri("https://api.resend.com/");
            client.Timeout = TimeSpan.FromSeconds(15);
            client.MaxResponseContentBufferSize = 64 * 1024;
        });
        services.AddScoped<IFilaNotificacoesServico, FilaNotificacoesServico>();
        services.AddHostedService<NotificacoesWorker>();
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
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();

        return services;
    }
}
