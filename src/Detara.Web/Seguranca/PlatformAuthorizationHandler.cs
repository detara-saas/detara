using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Detara.Web.Seguranca;

public sealed class PlatformAuthorizationHandler(
    PlatformTokenStorage tokenStorage,
    SessaoRefreshServico refreshServico) : DelegatingHandler
{
    public Uri? ApiBaseAddress { get; set; }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var ehApi = EhDestinoDaApi(request.RequestUri);
        var ehProtegida = ehApi && EhRotaProtegida(request.RequestUri);
        var token = ehProtegida ? await tokenStorage.ObterTokenAsync() : null;
        var copiaParaRetry = ehProtegida
            ? await ClonarAsync(request, cancellationToken)
            : null;
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        if (ehApi)
        {
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
        }

        var resposta = await base.SendAsync(request, cancellationToken);
        if (ehProtegida && resposta.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refresh = await refreshServico.RenovarPlataformaAsync(cancellationToken);
            if (refresh == ResultadoRefresh.Sucesso && copiaParaRetry is not null)
            {
                resposta.Dispose();
                copiaParaRetry.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer",
                    await tokenStorage.ObterTokenAsync());
                copiaParaRetry.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
                var repetida = await base.SendAsync(copiaParaRetry, cancellationToken);
                if (repetida.StatusCode == HttpStatusCode.Unauthorized)
                {
                    await tokenStorage.RemoverTokenAsync();
                }

                return repetida;
            }

            if (refresh == ResultadoRefresh.Invalido)
            {
                await tokenStorage.RemoverTokenAsync();
            }
        }

        return resposta;
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
            !caminho.Equals("/api/plataforma/autenticacao/mfa/verificar", StringComparison.OrdinalIgnoreCase) &&
            !caminho.Equals("/api/plataforma/autenticacao/refresh", StringComparison.OrdinalIgnoreCase) &&
            !caminho.Equals("/api/plataforma/autenticacao/logout", StringComparison.OrdinalIgnoreCase);
    }
}
