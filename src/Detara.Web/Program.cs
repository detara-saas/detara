using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Detara.Web;
using Detara.Web.Seguranca;
using Detara.Web.Servicos;
using Microsoft.AspNetCore.Components.Authorization;
using MudBlazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var apiBaseAddress = new Uri(
    builder.Configuration["Api:BaseUrl"] ?? builder.HostEnvironment.BaseAddress);
builder.Services.AddAuthorizationCore();
builder.Services.AddMudServices();
builder.Services.AddScoped<TokenStorage>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
    provider.GetRequiredService<JwtAuthenticationStateProvider>());
builder.Services.AddScoped<TokenAuthorizationHandler>();
builder.Services.AddScoped(provider =>
{
    var authorizationHandler = provider.GetRequiredService<TokenAuthorizationHandler>();
    authorizationHandler.ApiBaseAddress = apiBaseAddress;
    authorizationHandler.InnerHandler = new HttpClientHandler();
    return new HttpClient(authorizationHandler) { BaseAddress = apiBaseAddress };
});
builder.Services.AddScoped<AutenticacaoServico>();
builder.Services.AddScoped<IMensagemServico, MensagemServico>();

await builder.Build().RunAsync();
