using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Detara.Web.Servicos;

namespace Detara.UnitTests;

public sealed class AdministracaoTenantServicoTests
{
    [Fact]
    public async Task ObterMinhaConta_RespostaVaziaNaoAutorizada_RetornaFalhaSemExcecao()
    {
        using var http = new HttpClient(new RespostaFixaHandler(
            new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new ByteArrayContent([])
            }))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var servico = new AdministracaoTenantServico(http);

        var resultado = await servico.ObterMinhaContaAsync();

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.Resultado);
    }

    [Fact]
    public async Task ObterMinhaConta_RespostaNaoJson_RetornaFalhaSemExcecao()
    {
        using var http = new HttpClient(new RespostaFixaHandler(
            new HttpResponseMessage(HttpStatusCode.BadGateway)
            {
                Content = new StringContent("indisponivel", Encoding.UTF8, "text/plain")
            }))
        {
            BaseAddress = new Uri("http://localhost/")
        };
        var servico = new AdministracaoTenantServico(http);

        var resultado = await servico.ObterMinhaContaAsync();

        Assert.False(resultado.Sucesso);
        Assert.Equal("A API retornou uma resposta inválida.", resultado.Mensagem);
    }

    [Fact]
    public async Task ObterLogoPreview_UsaEndpointAutenticadoVersionadoEGeraDataUrl()
    {
        HttpRequestMessage? requestCapturado = null;
        using var http = new HttpClient(new RespostaHandler(request =>
        {
            requestCapturado = request;
            var conteudo = new ByteArrayContent([0x89, 0x50, 0x4E, 0x47]);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("image/png");
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = conteudo };
        }))
        {
            BaseAddress = new Uri("https://api.detara.test/")
        };
        var servico = new AdministracaoTenantServico(http);

        var resultado = await servico.ObterLogoPreviewAsync(7);

        Assert.True(resultado.Sucesso);
        Assert.Equal("data:image/png;base64,iVBORw==", resultado.Resultado);
        Assert.Equal("https://api.detara.test/api/empresa/logo?v=7",
            requestCapturado?.RequestUri?.ToString());
    }

    [Fact]
    public async Task ObterLogoPreview_ConteudoInvalidoRetornaFallbackSemExcecao()
    {
        using var http = new HttpClient(new RespostaHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("nao-e-imagem", Encoding.UTF8, "text/plain")
            }))
        {
            BaseAddress = new Uri("https://api.detara.test/")
        };

        var resultado = await new AdministracaoTenantServico(http).ObterLogoPreviewAsync(1);

        Assert.False(resultado.Sucesso);
        Assert.Null(resultado.Resultado);
    }

    private sealed class RespostaFixaHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }

    private sealed class RespostaHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(responder(request));
    }
}
