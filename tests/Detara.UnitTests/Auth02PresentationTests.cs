namespace Detara.UnitTests;

public sealed class Auth02PresentationTests
{
    [Fact]
    public void Login_ExplicitaRememberMeDesmarcadoEAutocompleteSeguro()
    {
        var pagina = Ler("src", "Detara.Web", "Pages", "Login.razor");

        Assert.Contains("Manter-me conectado", pagina, StringComparison.Ordinal);
        Assert.Contains("Permaneça conectado neste dispositivo.", pagina, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"username\"", pagina, StringComparison.Ordinal);
        Assert.Contains("autocomplete=\"current-password\"", pagina, StringComparison.Ordinal);
        Assert.Contains("private bool _manterConectado;", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", pagina, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ServiceWorker_MantemApiEAutenticacaoForaDoCache()
    {
        var worker = Ler("src", "Detara.Web", "wwwroot", "service-worker.published.js");

        Assert.Contains("requestUrl.pathname.startsWith(new URL('api/', baseUrl).pathname)", worker, StringComparison.Ordinal);
        Assert.Contains("return fetch(request);", worker, StringComparison.Ordinal);
    }

    [Fact]
    public void RefreshToken_NaoEhArmazenadoEmWebStorage()
    {
        var storage = Ler("src", "Detara.Web", "Seguranca", "TokenStorage.cs");
        var refresh = Ler("src", "Detara.Web", "Seguranca", "SessaoRefreshServico.cs");

        Assert.Contains("sessionStorage", storage, StringComparison.Ordinal);
        Assert.DoesNotContain("localStorage", storage, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", storage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localStorage", refresh, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionStorage", refresh, StringComparison.OrdinalIgnoreCase);
    }

    private static string Ler(params string[] partes) =>
        File.ReadAllText(Path.Combine([EncontrarRaizRepositorio(), .. partes]));

    private static string EncontrarRaizRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Detara.sln")))
        {
            atual = atual.Parent;
        }

        return atual?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
