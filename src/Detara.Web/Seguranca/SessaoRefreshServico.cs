using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Autenticacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Plataforma;
using Microsoft.AspNetCore.Components.WebAssembly.Http;

namespace Detara.Web.Seguranca;

public enum ResultadoRefresh
{
    Sucesso,
    Invalido,
    Indisponivel
}

public sealed class SessaoRefreshServico(
    HttpClientAutenticacao cliente,
    TokenStorage tokenStorage,
    PlatformTokenStorage platformTokenStorage)
{
    private readonly object _sincronizacao = new();
    private Task<ResultadoRefresh>? _refreshTenant;
    private Task<ResultadoRefresh>? _refreshPlataforma;

    public Task<ResultadoRefresh> RenovarTenantAsync(CancellationToken cancellationToken = default)
    {
        lock (_sincronizacao)
        {
            return _refreshTenant ??= ExecutarELiberarTenantAsync(cancellationToken);
        }
    }

    public Task<ResultadoRefresh> RenovarPlataformaAsync(CancellationToken cancellationToken = default)
    {
        lock (_sincronizacao)
        {
            return _refreshPlataforma ??= ExecutarELiberarPlataformaAsync(cancellationToken);
        }
    }

    private async Task<ResultadoRefresh> ExecutarELiberarTenantAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            return await RenovarAsync<LoginAutenticadoResponse>(
                "api/autenticacao/refresh",
                sessao => tokenStorage.SalvarAsync(sessao.Token).AsTask(),
                cancellationToken);
        }
        finally
        {
            lock (_sincronizacao)
            {
                _refreshTenant = null;
            }
        }
    }

    private async Task<ResultadoRefresh> ExecutarELiberarPlataformaAsync(CancellationToken cancellationToken)
    {
        await Task.Yield();
        try
        {
            return await RenovarAsync<SessaoPlataformaResponse>(
                "api/plataforma/autenticacao/refresh",
                sessao => platformTokenStorage.SalvarTokenAsync(sessao.Token).AsTask(),
                cancellationToken);
        }
        finally
        {
            lock (_sincronizacao)
            {
                _refreshPlataforma = null;
            }
        }
    }

    private async Task<ResultadoRefresh> RenovarAsync<T>(
        string rota,
        Func<T, Task> salvar,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, rota);
            request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
            using var response = await cliente.Valor.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                return ResultadoRefresh.Invalido;
            }

            if (!response.IsSuccessStatusCode)
            {
                return ResultadoRefresh.Indisponivel;
            }

            var resposta = await response.Content.ReadFromJsonAsync<RespostaApi<T>>(cancellationToken);
            if (resposta is not { Sucesso: true, Resultado: not null })
            {
                return ResultadoRefresh.Invalido;
            }

            await salvar(resposta.Resultado);
            return ResultadoRefresh.Sucesso;
        }
        catch (HttpRequestException)
        {
            return ResultadoRefresh.Indisponivel;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoRefresh.Indisponivel;
        }
    }
}

public sealed class HttpClientAutenticacao(HttpClient valor)
{
    public HttpClient Valor { get; } = valor;
}
