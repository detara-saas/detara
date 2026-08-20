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

    [Theory]
    [InlineData("api/plataforma/dashboard")]
    [InlineData("api/convites/administrador/validar")]
    public async Task TokenTenant_NuncaEhEnviadoAIdentidadesSeparadas(string rota)
    {
        var captura = new CapturaAuthorizationHandler();
        var contexto = CriarContexto(captura);
        await contexto.Storage.SalvarAsync("token-tenant");

        await contexto.Http.GetAsync(rota);

        Assert.Null(captura.Authorization);
        Assert.Equal("token-tenant", await contexto.Storage.ObterAsync());
    }

    [Fact]
    public async Task TokenPlataforma_EhEnviadoSomenteAoEspacoProtegidoDaPlataforma()
    {
        var js = new StorageJsRuntime();
        var storage = new PlatformTokenStorage(js);
        await storage.SalvarTokenAsync("token-platform");
        var captura = new CapturaAuthorizationHandler();
        var handler = new PlatformAuthorizationHandler(storage)
        {
            ApiBaseAddress = ApiBaseAddress,
            InnerHandler = captura
        };
        using var http = new HttpClient(handler) { BaseAddress = ApiBaseAddress };

        await http.GetAsync("api/plataforma/dashboard");
        Assert.Equal("Bearer token-platform", captura.Authorization);

        captura.Authorization = null;
        await http.PostAsync("api/plataforma/autenticacao/login", null);
        Assert.Null(captura.Authorization);

        await http.PostAsync("api/convites/administrador/validar", null);
        Assert.Null(captura.Authorization);
    }

    [Fact]
    public async Task Resposta401Plataforma_RemoveSomenteTokenPlataforma()
    {
        var js = new StorageJsRuntime();
        var storage = new PlatformTokenStorage(js);
        await storage.SalvarTokenAsync("token-platform");
        var handler = new PlatformAuthorizationHandler(storage)
        {
            ApiBaseAddress = ApiBaseAddress,
            InnerHandler = new RespostaHandler(HttpStatusCode.Unauthorized)
        };
        using var http = new HttpClient(handler) { BaseAddress = ApiBaseAddress };

        await http.GetAsync("api/plataforma/dashboard");

        Assert.Null(await storage.ObterTokenAsync());
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

    private sealed class CapturaAuthorizationHandler : HttpMessageHandler
    {
        public string? Authorization { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Authorization = request.Headers.Authorization?.ToString();
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private sealed class StorageJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string?> _valores = new(StringComparer.Ordinal);

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
                    _valores.TryGetValue(args?[0]?.ToString() ?? string.Empty, out var valor);
                    return ValueTask.FromResult((TValue)(object?)valor!);
                case "sessionStorage.setItem":
                    _valores[args?[0]?.ToString() ?? string.Empty] = args?[1]?.ToString();
                    break;
                case "sessionStorage.removeItem":
                    _valores.Remove(args?[0]?.ToString() ?? string.Empty);
                    break;
            }

            return ValueTask.FromResult(default(TValue)!);
        }
    }
}
