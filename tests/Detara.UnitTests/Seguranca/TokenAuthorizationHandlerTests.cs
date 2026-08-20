using System.Net;
using Detara.Web.Seguranca;
using Detara.Web.Servicos;
using Microsoft.JSInterop;

namespace Detara.UnitTests.Seguranca;

public sealed class TokenAuthorizationHandlerTests
{
    private static readonly Uri ApiBaseAddress = new("https://api.detara.test/");

    [Fact]
    public async Task FalhaDeRede_NaoRemoveTokenEIndicaServidorIndisponivel()
    {
        var contexto = CriarContexto(new FalhaRedeHandler());
        await contexto.Storage.SalvarAsync("token-ativo");

        await Assert.ThrowsAsync<HttpRequestException>(() => contexto.Http.GetAsync("api/clientes"));

        Assert.Equal("token-ativo", await contexto.Storage.ObterAsync());
        Assert.False(contexto.Pwa.ServidorDisponivel);
    }

    [Fact]
    public async Task Resposta401_RemoveTokenEConservaFluxoDeLogout()
    {
        var contexto = CriarContexto(new RespostaHandler(HttpStatusCode.Unauthorized));
        await contexto.Storage.SalvarAsync("token-expirado");

        var resposta = await contexto.Http.GetAsync("api/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Null(await contexto.Storage.ObterAsync());
        Assert.True(contexto.Pwa.ServidorDisponivel);
    }

    [Fact]
    public async Task Timeout_NaoRemoveTokenESeTornaFalhaDeComunicacaoControlada()
    {
        var contexto = CriarContexto(new TimeoutHandler());
        await contexto.Storage.SalvarAsync("token-ativo");

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => contexto.Http.GetAsync("api/clientes"));

        Assert.IsType<TaskCanceledException>(exception.InnerException);
        Assert.Equal("token-ativo", await contexto.Storage.ObterAsync());
        Assert.False(contexto.Pwa.ServidorDisponivel);
    }

    [Fact]
    public async Task RespostaDaApi_AposFalhaMarcaServidorComoDisponivel()
    {
        var js = new StorageJsRuntime();
        var storage = new TokenStorage(js);
        var autenticacao = new JwtAuthenticationStateProvider(storage);
        var pwa = new PwaServico(js);
        pwa.RegistrarFalhaApi();
        var handler = new TokenAuthorizationHandler(storage, autenticacao, pwa)
        {
            ApiBaseAddress = ApiBaseAddress,
            InnerHandler = new RespostaHandler(HttpStatusCode.OK)
        };
        using var http = new HttpClient(handler) { BaseAddress = ApiBaseAddress };

        await http.GetAsync("api/clientes");

        Assert.True(pwa.ServidorDisponivel);
    }

    private static ContextoTeste CriarContexto(HttpMessageHandler innerHandler)
    {
        var js = new StorageJsRuntime();
        var storage = new TokenStorage(js);
        var autenticacao = new JwtAuthenticationStateProvider(storage);
        var pwa = new PwaServico(js);
        var handler = new TokenAuthorizationHandler(storage, autenticacao, pwa)
        {
            ApiBaseAddress = ApiBaseAddress,
            InnerHandler = innerHandler
        };
        var http = new HttpClient(handler) { BaseAddress = ApiBaseAddress };
        return new ContextoTeste(http, storage, pwa);
    }

    private sealed record ContextoTeste(HttpClient Http, TokenStorage Storage, PwaServico Pwa);

    private sealed class FalhaRedeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new HttpRequestException("API indisponível");
    }

    private sealed class RespostaHandler(HttpStatusCode statusCode) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(statusCode));
    }

    private sealed class TimeoutHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            throw new TaskCanceledException("Timeout simulado");
    }

    private sealed class StorageJsRuntime : IJSRuntime
    {
        private string? _token;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            switch (identifier)
            {
                case "sessionStorage.getItem":
                    return ValueTask.FromResult((TValue)(object?)_token!);
                case "sessionStorage.setItem":
                    _token = args?[1]?.ToString();
                    break;
                case "sessionStorage.removeItem":
                    _token = null;
                    break;
            }

            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
