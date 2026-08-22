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
using Detara.Application.Comunicacao;
using Detara.Application.Plataforma;
using Detara.Application.Onboarding;
using Detara.Application.AdministracaoTenant;
using Detara.Application.Dashboard;
using Detara.Infrastructure.AdministracaoTenant;
using Detara.Infrastructure.Notificacoes;
using Detara.Domain.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Amazon.Runtime;
using Amazon.S3;

namespace Detara.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        AplicarAliasesConfiguracaoEmail(configuration);
        var connectionString = configuration.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "A connection string 'DefaultConnection' deve ser configurada por secret ou variável de ambiente.");
        }

        services.AddDbContext<DetaraDbContext>(options => options.UseSqlServer(connectionString));
        services.AddMemoryCache();
        services.AddScoped<IConsultaIdentidadeLoginTenant, UsuarioAutenticacaoRepositorio>();
        services.AddScoped<IChallengeSelecaoEmpresaTenant, ChallengeSelecaoEmpresaTenant>();
        services.AddSingleton<ISenhaServico, SenhaServico>();
        services.AddScoped<IValidadorIdentidadeAutenticada, ValidadorIdentidadeAutenticada>();
        services.AddScoped<IAutenticacaoPlataformaServico, AutenticacaoPlataformaServico>();
        services.AddScoped<IAdministracaoPlataformaServico, AdministracaoPlataformaServico>();
        services.AddScoped<IAdministracaoEmpresaTenantServico, AdministracaoEmpresaTenantServico>();
        services.AddScoped<IAdministracaoUsuariosTenantServico, AdministracaoUsuariosTenantServico>();
        services.AddScoped<IAdministracaoPerfisTenantServico, AdministracaoPerfisTenantServico>();
        services.AddScoped<IMinhaContaTenantServico, MinhaContaTenantServico>();
        services.AddScoped<IConvitesAdministradoresEmpresaServico, ConvitesAdministradoresEmpresaServico>();
        services.AddScoped<IFilaConvitesAdministradoresEmpresaServico, FilaConvitesAdministradoresEmpresaServico>();
        services.AddHostedService<ConvitesAdministradoresEmpresaWorker>();
        services.Configure<PlataformaOptions>(configuration.GetSection(PlataformaOptions.Secao));
        services.Configure<WebPublicaOptions>(configuration.GetSection(WebPublicaOptions.Secao));
        services.AddScoped<IPreferenciasUsuarioRepositorio, PreferenciasUsuarioRepositorio>();
        services.AddScoped<IPlataformaOnboardingConsulta, PlataformaOnboardingConsulta>();
        services.AddScoped<IAtendimentoOnboardingConsulta, AtendimentoOnboardingConsulta>();
        services.AddScoped<ICatalogoOnboardingConsulta, CatalogoOnboardingConsulta>();
        services.AddScoped<IClientesOnboardingConsulta, ClientesOnboardingConsulta>();
        services.AddScoped<IAgendaOnboardingConsulta, AgendaOnboardingConsulta>();
        services.AddScoped<IPlataformaDashboardConsulta, PlataformaDashboardConsulta>();
        services.AddScoped<IAgendaDashboardConsulta, AgendaDashboardConsulta>();
        services.AddScoped<IAtendimentoDashboardConsulta, AtendimentoDashboardConsulta>();
        services.AddScoped<IFinanceiroDashboardConsulta, FinanceiroDashboardConsulta>();
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
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Secao));
        if (string.Equals(storageOptions.Provider, "Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IArquivoStorage, LocalArquivoStorage>();
        }
        else if (string.Equals(storageOptions.Provider, "S3", StringComparison.OrdinalIgnoreCase))
        {
            ValidarStorageS3(storageOptions.S3);
            var s3Config = new AmazonS3Config
            {
                ServiceURL = storageOptions.S3.ServiceUrl,
                AuthenticationRegion = storageOptions.S3.Region,
                ForcePathStyle = storageOptions.S3.ForcePathStyle,
                Timeout = TimeSpan.FromSeconds(15),
                MaxErrorRetry = 2
            };
            services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
                new BasicAWSCredentials(storageOptions.S3.AccessKey, storageOptions.S3.SecretKey),
                s3Config));
            services.AddSingleton<IS3ObjectClient, AwsS3ObjectClient>();
            services.AddSingleton<IArquivoStorage, S3ArquivoStorage>();
        }
        else
        {
            throw new InvalidOperationException(
                $"O provider de storage '{storageOptions.Provider}' não é suportado.");
        }
        services.AddSingleton<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
        services.AddSingleton<IPasswordHasher<AdministradorPlataforma>, PasswordHasher<AdministradorPlataforma>>();

        return services;
    }

    private static void ValidarStorageS3(S3StorageOptions options)
    {
        if (!Uri.TryCreate(options.ServiceUrl, UriKind.Absolute, out var endpoint) ||
            endpoint.Scheme != Uri.UriSchemeHttps ||
            string.IsNullOrWhiteSpace(options.Bucket) ||
            string.IsNullOrWhiteSpace(options.Region) ||
            string.IsNullOrWhiteSpace(options.AccessKey) ||
            string.IsNullOrWhiteSpace(options.SecretKey))
        {
            throw new InvalidOperationException(
                "Storage:S3 exige endpoint HTTPS, bucket, região e credenciais.");
        }
    }

    private static void AplicarAliasesConfiguracaoEmail(IConfiguration configuration)
    {
        AplicarAlias(configuration, "DETARA_RESEND_API_KEY", "Email:ApiKey");
        AplicarAlias(configuration, "DETARA_EMAIL_FROM_ADDRESS", "Email:FromAddress");
    }

    private static void AplicarAlias(
        IConfiguration configuration,
        string origem,
        string destino)
    {
        var valor = configuration[origem];
        if (!string.IsNullOrWhiteSpace(valor))
        {
            configuration[destino] = valor;
        }
    }
}
