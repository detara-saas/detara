using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Autenticacao;
using Detara.Contracts.Comum;
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
        Assert.Equal(EstadoConectividade.Falha, contexto.Pwa.Conectividade);
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

    [Theory]
    [InlineData(HttpStatusCode.PaymentRequired)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task Resposta402Ou403_NaoDisparaRefresh(HttpStatusCode statusCode)
    {
        var refreshHandler = new RefreshContadorHandler(sucesso: true);
        var contexto = CriarContexto(new RespostaHandler(statusCode), refreshHandler);
        await contexto.Storage.SalvarAsync("token-ativo");

        var resposta = await contexto.Http.GetAsync("api/clientes");

        Assert.Equal(statusCode, resposta.StatusCode);
        Assert.Equal(0, refreshHandler.Chamadas);
        Assert.Equal("token-ativo", await contexto.Storage.ObterAsync());
    }

    [Fact]
    public async Task Resposta401_RefreshComSucessoRepeteRequestUmaUnicaVez()
    {
        var refreshHandler = new RefreshContadorHandler(sucesso: true);
        var operacao = new OperacaoProtegidaHandler();
        var contexto = CriarContexto(operacao, refreshHandler);
        await contexto.Storage.SalvarAsync("token-expirado");

        var resposta = await contexto.Http.GetAsync("api/clientes");

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        Assert.Equal(1, refreshHandler.Chamadas);
        Assert.Equal(2, operacao.Chamadas);
        Assert.Equal("novo-token", await contexto.Storage.ObterAsync());
    }

    [Fact]
    public async Task Multiplos401Simultaneos_UsamUmUnicoRefresh()
    {
        var refreshHandler = new RefreshContadorHandler(sucesso: true, atraso: true);
        var operacao = new OperacaoProtegidaHandler();
        var contexto = CriarContexto(operacao, refreshHandler);
        await contexto.Storage.SalvarAsync("token-expirado");

        var respostas = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => contexto.Http.GetAsync("api/clientes")));

        Assert.All(respostas, resposta => Assert.Equal(HttpStatusCode.OK, resposta.StatusCode));
        Assert.Equal(1, refreshHandler.Chamadas);
        Assert.Equal(10, operacao.Chamadas);
    }

    [Fact]
    public async Task FalhaDeRedeNoRefresh_NaoApagaSessaoLocal()
    {
        var contexto = CriarContexto(
            new RespostaHandler(HttpStatusCode.Unauthorized),
            new RefreshContadorHandler(sucesso: false, falhaRede: true));
        await contexto.Storage.SalvarAsync("token-expirado");

        var resposta = await contexto.Http.GetAsync("api/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        Assert.Equal("token-expirado", await contexto.Storage.ObterAsync());
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
        var refresh = CriarRefresh(js, storage);
        var autenticacao = new JwtAuthenticationStateProvider(storage, refresh);
        var pwa = new PwaServico(js);
        pwa.RegistrarFalhaApi();
        var handler = new TokenAuthorizationHandler(storage, autenticacao, refresh, pwa)
        {
            ApiBaseAddress = ApiBaseAddress,
            InnerHandler = new RespostaHandler(HttpStatusCode.OK)
        };
        using var http = new HttpClient(handler) { BaseAddress = ApiBaseAddress };

        await http.GetAsync("api/clientes");

        Assert.True(pwa.ServidorDisponivel);
        Assert.Equal(EstadoConectividade.Restabelecida, pwa.Conectividade);
    }

    [Fact]
    public async Task NavegadorVoltaDaRede_VerificaAntesDeDeclararRestabelecimento()
    {
        var pwa = new PwaServico(new StorageJsRuntime());

        await pwa.AtualizarEstadoPwaAsync(false, false, false, false);
        Assert.Equal(EstadoConectividade.Offline, pwa.Conectividade);

        await pwa.AtualizarEstadoPwaAsync(true, false, false, false);
        Assert.Equal(EstadoConectividade.Reconectando, pwa.Conectividade);
        Assert.False(pwa.ServidorDisponivel);

        pwa.RegistrarRespostaApi();
        Assert.Equal(EstadoConectividade.Restabelecida, pwa.Conectividade);

        await pwa.DisposeAsync();
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
        var refresh = CriarRefresh(js, platformStorage: storage);
        var handler = new PlatformAuthorizationHandler(storage, refresh)
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
        var refresh = CriarRefresh(js, platformStorage: storage);
        var handler = new PlatformAuthorizationHandler(storage, refresh)
        {
            ApiBaseAddress = ApiBaseAddress,
            InnerHandler = new RespostaHandler(HttpStatusCode.Unauthorized)
        };
        using var http = new HttpClient(handler) { BaseAddress = ApiBaseAddress };

        await http.GetAsync("api/plataforma/dashboard");

        Assert.Null(await storage.ObterTokenAsync());
    }

    private static ContextoTeste CriarContexto(
        HttpMessageHandler innerHandler,
        HttpMessageHandler? refreshHandler = null)
    {
        var js = new StorageJsRuntime();
        var storage = new TokenStorage(js);
        var refresh = CriarRefresh(js, storage, refreshHandler: refreshHandler);
        var autenticacao = new JwtAuthenticationStateProvider(storage, refresh);
        var pwa = new PwaServico(js);
        var handler = new TokenAuthorizationHandler(storage, autenticacao, refresh, pwa)
        {
            ApiBaseAddress = ApiBaseAddress,
            InnerHandler = innerHandler
        };
        var http = new HttpClient(handler) { BaseAddress = ApiBaseAddress };
        return new ContextoTeste(http, storage, pwa);
    }

    private static SessaoRefreshServico CriarRefresh(
        StorageJsRuntime js,
        TokenStorage? storage = null,
        PlatformTokenStorage? platformStorage = null,
        HttpMessageHandler? refreshHandler = null)
    {
        storage ??= new TokenStorage(js);
        platformStorage ??= new PlatformTokenStorage(js);
        var http = new HttpClient(refreshHandler ?? new RespostaHandler(HttpStatusCode.Unauthorized))
        {
            BaseAddress = ApiBaseAddress
        };
        return new SessaoRefreshServico(
            new HttpClientAutenticacao(http),
            storage,
            platformStorage);
    }

    private sealed class OperacaoProtegidaHandler : HttpMessageHandler
    {
        public int Chamadas;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Chamadas);
            var status = request.Headers.Authorization?.Parameter == "novo-token"
                ? HttpStatusCode.OK
                : HttpStatusCode.Unauthorized;
            return Task.FromResult(new HttpResponseMessage(status));
        }
    }

    private sealed class RefreshContadorHandler(
        bool sucesso,
        bool atraso = false,
        bool falhaRede = false) : HttpMessageHandler
    {
        public int Chamadas;

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Chamadas);
            if (atraso) await Task.Delay(50, cancellationToken);
            if (falhaRede) throw new HttpRequestException("offline");
            if (!sucesso) return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(RespostaApi<LoginAutenticadoResponse>.Ok(new(
                    "novo-token",
                    DateTime.UtcNow.AddMinutes(15),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Usuário",
                    "Administrador",
                    [])))
            };
        }
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
