using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Microsoft.Extensions.Options;

namespace Detara.Api.Autenticacao;

public sealed class RefreshCookieServico(
    IOptions<SessaoAutenticacaoOptions> options,
    IWebHostEnvironment environment,
    IConfiguration configuration)
{
    private const string CaminhoTenant = "/api/autenticacao";
    private const string CaminhoPlataforma = "/api/plataforma/autenticacao";
    private readonly SessaoAutenticacaoOptions _options = options.Value;
    private readonly HashSet<string> _origensPermitidas = configuration
        .GetSection("Cors:OrigensPermitidas")
        .Get<string[]>()?
        .ToHashSet(StringComparer.OrdinalIgnoreCase) ?? [];

    public string? ObterTenant(HttpRequest request) =>
        request.Cookies[_options.CookieTenant];

    public string? ObterPlataforma(HttpRequest request) =>
        request.Cookies[_options.CookiePlataforma];

    public void EscreverTenant(HttpResponse response, RefreshTokenCriado token) =>
        Escrever(response, _options.CookieTenant, CaminhoTenant, token);

    public void EscreverPlataforma(HttpResponse response, RefreshTokenCriado token) =>
        Escrever(response, _options.CookiePlataforma, CaminhoPlataforma, token);

    public void LimparTenant(HttpResponse response) =>
        Limpar(response, _options.CookieTenant, CaminhoTenant);

    public void LimparPlataforma(HttpResponse response) =>
        Limpar(response, _options.CookiePlataforma, CaminhoPlataforma);

    public bool OrigemPermitida(HttpRequest request)
    {
        if (!request.Headers.TryGetValue("Origin", out var origem) ||
            string.IsNullOrWhiteSpace(origem))
        {
            return true;
        }

        return _origensPermitidas.Contains(origem.ToString());
    }

    private void Escrever(
        HttpResponse response,
        string nome,
        string caminho,
        RefreshTokenCriado token)
    {
        var cookie = CriarOpcoes(caminho);
        if (token.Persistente)
        {
            cookie.Expires = new DateTimeOffset(DateTime.SpecifyKind(token.ExpiraEmUtc, DateTimeKind.Utc));
        }

        response.Cookies.Append(nome, token.Valor, cookie);
    }

    private void Limpar(HttpResponse response, string nome, string caminho) =>
        response.Cookies.Delete(nome, CriarOpcoes(caminho));

    private CookieOptions CriarOpcoes(string caminho) => new()
    {
        HttpOnly = true,
        Secure = !environment.IsDevelopment(),
        SameSite = SameSiteMode.Lax,
        IsEssential = true,
        Path = caminho
    };
}
