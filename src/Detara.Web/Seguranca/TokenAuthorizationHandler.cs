using System.Net;
using System.Net.Http.Headers;
using Detara.Web.Servicos;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Detara.Web.Seguranca;

public sealed class TokenAuthorizationHandler(
    TokenStorage tokenStorage,
    JwtAuthenticationStateProvider authenticationStateProvider,
    SessaoRefreshServico refreshServico,
    PwaServico pwa,
    NavigationManager? navigation = null)
    : DelegatingHandler
{
    public Uri? ApiBaseAddress { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var token = await tokenStorage.ObterAsync();
        var destinoDaApi = EhDestinoDaApi(request.RequestUri) &&
            !EhRotaDeIdentidadeSeparada(request.RequestUri);
        var elegivelRefresh = destinoDaApi && EhRotaElegivelParaRefresh(request.RequestUri);
        var copiaParaRetry = elegivelRefresh
            ? await ClonarAsync(request, cancellationToken)
            : null;
        if (!string.IsNullOrWhiteSpace(token) && destinoDaApi)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (destinoDaApi)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
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

        if (response.StatusCode == HttpStatusCode.Unauthorized && elegivelRefresh)
        {
            var refresh = await refreshServico.RenovarTenantAsync(cancellationToken);
            if (refresh == ResultadoRefresh.Sucesso && copiaParaRetry is not null)
            {
                response.Dispose();
                var novoToken = await tokenStorage.ObterAsync();
                copiaParaRetry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", novoToken);
                copiaParaRetry.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
                var repetida = await base.SendAsync(copiaParaRetry, cancellationToken);
                pwa.RegistrarRespostaApi();
                if (repetida.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await tokenStorage.RemoverAsync();
                    authenticationStateProvider.NotificarLogout();
                }

                return repetida;
            }

            if (refresh == ResultadoRefresh.Invalido)
            {
                await tokenStorage.RemoverAsync();
                authenticationStateProvider.NotificarLogout();
            }
        }

        if (response.StatusCode == HttpStatusCode.PaymentRequired && destinoDaApi && navigation is not null &&
            !navigation.Uri.Contains("/assinatura-suspensa", StringComparison.OrdinalIgnoreCase))
        {
            navigation.NavigateTo("/assinatura-suspensa", replace: true);
        }

        return response;
    }

    private static bool EhRotaElegivelParaRefresh(Uri? destino)
    {
        var caminho = destino?.AbsolutePath ?? string.Empty;
        return !caminho.StartsWith("/api/autenticacao", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<HttpRequestMessage> ClonarAsync(
        HttpRequestMessage original,
        CancellationToken cancellationToken)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Version = original.Version,
            VersionPolicy = original.VersionPolicy
        };
        foreach (var header in original.Headers)
        {
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        foreach (var option in original.Options)
        {
            clone.Options.Set(new HttpRequestOptionsKey<object?>(option.Key), option.Value);
        }

        if (original.Content is not null)
        {
            var bytes = await original.Content.ReadAsByteArrayAsync(cancellationToken);
            clone.Content = new ByteArrayContent(bytes);
            foreach (var header in original.Content.Headers)
            {
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }
        }

        return clone;
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

    private static bool EhRotaDeIdentidadeSeparada(Uri? destino)
    {
        var caminho = destino?.AbsolutePath ?? string.Empty;
        return caminho.StartsWith("/api/plataforma", StringComparison.OrdinalIgnoreCase) ||
            caminho.StartsWith("/api/convites/administrador", StringComparison.OrdinalIgnoreCase);
    }
}
