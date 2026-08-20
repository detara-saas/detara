using System.Net;
using System.Net.Http.Headers;

namespace Detara.Web.Seguranca;

public sealed class PlatformAuthorizationHandler(PlatformTokenStorage tokenStorage) : DelegatingHandler
{
    public Uri? ApiBaseAddress { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var ehApi = EhDestinoDaApi(request.RequestUri);
        var ehProtegida = ehApi && EhRotaProtegida(request.RequestUri);
        var token = ehProtegida ? await tokenStorage.ObterTokenAsync() : null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var resposta = await base.SendAsync(request, cancellationToken);
        if (ehProtegida && resposta.StatusCode == HttpStatusCode.Unauthorized)
        {
            await tokenStorage.RemoverTokenAsync();
        }

        return resposta;
    }

    private bool EhDestinoDaApi(Uri? destino) =>
        ApiBaseAddress is not null &&
        destino is { IsAbsoluteUri: true } &&
        Uri.Compare(
            ApiBaseAddress,
            destino,
            UriComponents.SchemeAndServer,
            UriFormat.Unescaped,
            StringComparison.OrdinalIgnoreCase) == 0;

    private static bool EhRotaProtegida(Uri? destino)
    {
        var caminho = destino?.AbsolutePath ?? string.Empty;
        if (!caminho.StartsWith("/api/plataforma", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !caminho.Equals("/api/plataforma/autenticacao/login", StringComparison.OrdinalIgnoreCase) &&
            !caminho.Equals("/api/plataforma/autenticacao/mfa/configuracao", StringComparison.OrdinalIgnoreCase) &&
            !caminho.Equals("/api/plataforma/autenticacao/mfa/ativar", StringComparison.OrdinalIgnoreCase) &&
            !caminho.Equals("/api/plataforma/autenticacao/mfa/verificar", StringComparison.OrdinalIgnoreCase);
    }
}
