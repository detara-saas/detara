using System.Net;
using System.Net.Http.Headers;
using Detara.Web.Servicos;

namespace Detara.Web.Seguranca;

public sealed class TokenAuthorizationHandler(
    TokenStorage tokenStorage,
    JwtAuthenticationStateProvider authenticationStateProvider,
    PwaServico pwa)
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

        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
            if (destinoDaApi)
            {
                pwa.RegistrarRespostaApi();
            }
        }
        catch (HttpRequestException) when (destinoDaApi)
        {
            pwa.RegistrarFalhaApi();
            throw;
        }
        catch (TaskCanceledException exception) when (
            destinoDaApi && !cancellationToken.IsCancellationRequested)
        {
            pwa.RegistrarFalhaApi();
            throw new HttpRequestException("A API não respondeu dentro do tempo esperado.", exception);
        }

        if (response.StatusCode == HttpStatusCode.Unauthorized &&
            !string.IsNullOrWhiteSpace(token) &&
            destinoDaApi)
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
