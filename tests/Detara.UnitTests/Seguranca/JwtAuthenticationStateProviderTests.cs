using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Detara.Contracts.Autenticacao;
using Detara.Contracts.Comum;
using Detara.Web.Seguranca;
using Microsoft.JSInterop;

namespace Detara.UnitTests.Seguranca;

public sealed class JwtAuthenticationStateProviderTests
{
    private static readonly Uri Api = new("https://api.detara.test/");

    [Fact]
    public async Task StartupComAccessTokenValido_NaoChamaRefresh()
    {
        var contexto = CriarContexto(HttpStatusCode.Unauthorized);
        await contexto.Storage.SalvarAsync(CriarJwt(DateTimeOffset.UtcNow.AddMinutes(10)));

        var estado = await contexto.Provider.GetAuthenticationStateAsync();

        Assert.True(estado.User.Identity?.IsAuthenticated);
        Assert.Equal(0, contexto.Refresh.Chamadas);
    }

    [Fact]
    public async Task StartupSemAccessToken_RestauraSessaoComRefreshValido()
    {
        var contexto = CriarContexto(HttpStatusCode.OK);

        var estado = await contexto.Provider.GetAuthenticationStateAsync();

        Assert.True(estado.User.Identity?.IsAuthenticated);
        Assert.Equal(1, contexto.Refresh.Chamadas);
        Assert.False(string.IsNullOrWhiteSpace(await contexto.Storage.ObterAsync()));
    }

    [Fact]
    public async Task StartupSemRefreshValido_PermaneceAnonimo()
    {
        var contexto = CriarContexto(HttpStatusCode.Unauthorized);

        var estado = await contexto.Provider.GetAuthenticationStateAsync();

        Assert.False(estado.User.Identity?.IsAuthenticated);
        Assert.Equal(1, contexto.Refresh.Chamadas);
        Assert.Null(await contexto.Storage.ObterAsync());
    }

    [Fact]
    public async Task ErroDeRedeNoRefresh_NaoRemoveAccessTokenExpiradoENaoFixaFalha()
    {
        var contexto = CriarContexto(null);
        var expirado = CriarJwt(DateTimeOffset.UtcNow.AddMinutes(-1));
        await contexto.Storage.SalvarAsync(expirado);

        Assert.False((await contexto.Provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);
        Assert.False((await contexto.Provider.GetAuthenticationStateAsync()).User.Identity?.IsAuthenticated);

        Assert.Equal(expirado, await contexto.Storage.ObterAsync());
        Assert.Equal(2, contexto.Refresh.Chamadas);
    }

    private static Contexto CriarContexto(HttpStatusCode? refreshStatus)
    {
        var js = new StorageJsRuntime();
        var storage = new TokenStorage(js);
        var platform = new PlatformTokenStorage(js);
        var handler = new RefreshHandler(refreshStatus);
        var http = new HttpClient(handler) { BaseAddress = Api };
        var refresh = new SessaoRefreshServico(
            new HttpClientAutenticacao(http),
            storage,
            platform);
        return new(storage, new JwtAuthenticationStateProvider(storage, refresh), handler);
    }

    private static string CriarJwt(DateTimeOffset expiraEm) =>
        $"{Codificar(new Dictionary<string, object> { ["alg"] = "none" })}." +
        $"{Codificar(new Dictionary<string, object>
        {
            ["sub"] = Guid.NewGuid().ToString(),
            ["name"] = "Usuário",
            ["exp"] = expiraEm.ToUnixTimeSeconds()
        })}.assinatura";

    private static string Codificar(object valor) => Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(valor)))
        .TrimEnd('=')
        .Replace('+', '-')
        .Replace('/', '_');

    private sealed record Contexto(
        TokenStorage Storage,
        JwtAuthenticationStateProvider Provider,
        RefreshHandler Refresh);

    private sealed class RefreshHandler(HttpStatusCode? status) : HttpMessageHandler
    {
        public int Chamadas;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Chamadas);
            if (status is null) throw new HttpRequestException("offline");
            var resposta = new HttpResponseMessage(status.Value);
            if (status == HttpStatusCode.OK)
            {
                resposta.Content = JsonContent.Create(RespostaApi<LoginAutenticadoResponse>.Ok(new(
                    CriarJwt(DateTimeOffset.UtcNow.AddMinutes(15)),
                    DateTime.UtcNow.AddMinutes(15),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "Usuário",
                    "Administrador",
                    [])));
            }

            return Task.FromResult(resposta);
        }
    }

    private sealed class StorageJsRuntime : IJSRuntime
    {
        private readonly Dictionary<string, string?> _valores = [];

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            var chave = args?[0]?.ToString() ?? string.Empty;
            object? valor = null;
            switch (identifier)
            {
                case "sessionStorage.getItem":
                    _valores.TryGetValue(chave, out var armazenado);
                    valor = armazenado;
                    break;
                case "sessionStorage.setItem":
                    _valores[chave] = args?[1]?.ToString();
                    break;
                case "sessionStorage.removeItem":
                    _valores.Remove(chave);
                    break;
            }

            return ValueTask.FromResult((TValue)valor!);
        }
    }
}
