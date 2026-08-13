using System.Net;
using System.Net.Http.Headers;

namespace Detara.Web.Seguranca;

public sealed class TokenAuthorizationHandler(
    TokenStorage tokenStorage,
    JwtAuthenticationStateProvider authenticationStateProvider)
    : DelegatingHandler
{
    public Uri? ApiBaseAddress { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenStorage.ObterAsync();
        var destinoDaApi = EhDestinoDaApi(request.RequestUri);
        if (!string.IsNullOrWhiteSpace(token) && destinoDaApi)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Unauthorized && destinoDaApi)
        {
            await tokenStorage.RemoverAsync();
            authenticationStateProvider.NotificarLogout();
        }

        return response;
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
}
