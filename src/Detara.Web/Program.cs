using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;
using Detara.Web;
using Detara.Web.Seguranca;
using Detara.Web.Servicos;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;
using Detara.Contracts.Autorizacao;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var culturaPadrao = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culturaPadrao;
CultureInfo.DefaultThreadCurrentUICulture = culturaPadrao;
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseUrl = builder.Configuration["Api:BaseUrl"];
var apiBaseAddress = new Uri(
    string.IsNullOrWhiteSpace(apiBaseUrl)
        ? builder.HostEnvironment.BaseAddress
        : apiBaseUrl);
builder.Services.AddAuthorizationCore(options =>
{
    foreach (var permissao in Permissoes.Todas)
    {
        options.AddPolicy(permissao, policy => policy.RequireClaim("permissao", permissao));
    }
});
builder.Services.AddLocalization();
builder.Services.AddMudServices();
builder.Services.AddScoped<TokenStorage>();
builder.Services.AddScoped<PlatformTokenStorage>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<TokenAuthorizationHandler>();
builder.Services.AddScoped<PlatformAuthorizationHandler>();
builder.Services.AddScoped(provider =>
{
    var authorizationHandler = provider.GetRequiredService<TokenAuthorizationHandler>();
    authorizationHandler.ApiBaseAddress = apiBaseAddress;
    authorizationHandler.InnerHandler = new HttpClientHandler();
    return new HttpClient(authorizationHandler) { BaseAddress = apiBaseAddress };
});
builder.Services.AddScoped(provider =>
{
    var authorizationHandler = provider.GetRequiredService<PlatformAuthorizationHandler>();
    authorizationHandler.ApiBaseAddress = apiBaseAddress;
    authorizationHandler.InnerHandler = new HttpClientHandler();
    return new HttpClientPlataforma(
        new HttpClient(authorizationHandler) { BaseAddress = apiBaseAddress });
});
builder.Services.AddScoped<AutenticacaoServico>();
builder.Services.AddScoped<IMensagemServico, MensagemServico>();
builder.Services.AddScoped<PreferenciasInterfaceServico>();
builder.Services.AddScoped<PwaServico>();
builder.Services.AddScoped<ClientesServico>();
builder.Services.AddScoped<VeiculosServico>();
builder.Services.AddScoped<CatalogoServico>();
builder.Services.AddScoped<AgendaServico>();
builder.Services.AddScoped<OrcamentosServico>();
builder.Services.AddScoped<OrdensServicoServico>();
builder.Services.AddScoped<ConfiguracoesServico>();
builder.Services.AddScoped<FinanceiroServico>();
builder.Services.AddScoped<NotificacoesServico>();
builder.Services.AddScoped<PlataformaServico>();
builder.Services.AddScoped<OnboardingServico>();
builder.Services.AddScoped<DashboardServico>();
builder.Services.AddScoped<AdministracaoTenantServico>();

var host = builder.Build();
await host.Services.GetRequiredService<PreferenciasInterfaceServico>().InicializarAsync();
await host.Services.GetRequiredService<PwaServico>().InicializarAsync();
await host.RunAsync();
