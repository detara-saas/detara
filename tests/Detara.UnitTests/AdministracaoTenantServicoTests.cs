using System.Net;
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

    private sealed class RespostaFixaHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(response);
    }
}
