using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using System.Globalization;
using Detara.Web;
using Detara.Web.Seguranca;
using Detara.Web.Servicos;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
var culturaPadrao = CultureInfo.GetCultureInfo("pt-BR");
CultureInfo.DefaultThreadCurrentCulture = culturaPadrao;
CultureInfo.DefaultThreadCurrentUICulture = culturaPadrao;
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = new Uri(
    builder.Configuration["Api:BaseUrl"] ?? builder.HostEnvironment.BaseAddress);
builder.Services.AddAuthorizationCore();
builder.Services.AddLocalization();
builder.Services.AddMudServices();
builder.Services.AddScoped<TokenStorage>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<TokenAuthorizationHandler>();
builder.Services.AddScoped(provider =>
{
    var handler = provider.GetRequiredService<TokenAuthorizationHandler>();
    handler.ApiBaseAddress = apiBaseAddress;
    handler.InnerHandler = new HttpClientHandler();
    return new HttpClient(handler) { BaseAddress = apiBaseAddress };
});
builder.Services.AddScoped<AutenticacaoServico>();
builder.Services.AddScoped<IMensagemServico, MensagemServico>();
builder.Services.AddScoped<PreferenciasInterfaceServico>();

var host = builder.Build();
await host.Services.GetRequiredService<PreferenciasInterfaceServico>().InicializarAsync();
await host.RunAsync();
