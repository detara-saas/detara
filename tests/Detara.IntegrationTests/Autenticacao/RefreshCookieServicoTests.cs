using Detara.Api.Autenticacao;
using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Autenticacao;

public sealed class RefreshCookieServicoTests
{
    [Fact]
    public void CookieDeSessao_EhHostOnlyHttpOnlyLaxSemExpiracaoPersistente()
    {
        var contexto = new DefaultHttpContext();
        var servico = CriarServico("Development");

        servico.EscreverTenant(
            contexto.Response,
            new RefreshTokenCriado("opaco", DateTime.UtcNow.AddHours(12), false));

        var cookie = contexto.Response.Headers.SetCookie.ToString();
        Assert.Contains("detara.refresh=opaco", cookie, StringComparison.Ordinal);
        Assert.Contains("path=/api/autenticacao", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CookiePersistenteEmProducao_EhSecureEExpiraComServidor()
    {
        var contexto = new DefaultHttpContext();
        var servico = CriarServico("Production");
        var expiracao = DateTime.UtcNow.AddDays(30);

        servico.EscreverTenant(
            contexto.Response,
            new RefreshTokenCriado("opaco", expiracao, true));

        var cookie = contexto.Response.Headers.SetCookie.ToString();
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("expires=", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("domain=", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("https://app.detara.com.br", true)]
    [InlineData("https://evil.example", false)]
    public void OrigemInformada_EhValidadaContraCorsExplicito(string origem, bool esperado)
    {
        var contexto = new DefaultHttpContext();
        contexto.Request.Headers.Origin = origem;

        Assert.Equal(esperado, CriarServico("Production").OrigemPermitida(contexto.Request));
    }

    private static RefreshCookieServico CriarServico(string ambiente)
    {
        var configuracao = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:OrigensPermitidas:0"] = "https://app.detara.com.br"
            })
            .Build();
        return new RefreshCookieServico(
            Options.Create(new SessaoAutenticacaoOptions()),
            new AmbienteTeste(ambiente),
            configuracao);
    }

    private sealed class AmbienteTeste(string nome) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Detara.Tests";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = string.Empty;
        public string EnvironmentName { get; set; } = nome;
        public string ContentRootPath { get; set; } = string.Empty;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
