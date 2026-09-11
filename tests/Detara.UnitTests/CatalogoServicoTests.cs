using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Catalogo;
using Detara.Contracts.Comum;
using Detara.Web.Servicos;

namespace Detara.UnitTests;

public sealed class CatalogoServicoTests
{
    [Fact]
    public async Task CriarPacote_QuandoApiRetornaValidacao_ExibeMensagemAcionavel()
    {
        var resposta = RespostaApi<PacoteDetalheResponse>.Falha(
            "Verifique os dados informados.",
            "validacao",
            new Dictionary<string, string[]>
            {
                ["Nome"] = ["O nome deve possuir no mínimo 2 caracteres."]
            });
        using var http = new HttpClient(new RespostaHandler(
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = JsonContent.Create(resposta)
            }))
        {
            BaseAddress = new Uri("http://localhost")
        };
        var servico = new CatalogoServico(http);

        var resultado = await servico.CriarPacoteAsync(new SalvarPacoteRequest(
            string.Empty,
            null,
            TipoPrecificacaoCatalogo.Fixo,
            null,
            []));

        Assert.False(resultado.Sucesso);
        Assert.Equal("O nome deve possuir no mínimo 2 caracteres.", resultado.Mensagem);
    }

    private sealed class RespostaHandler(HttpResponseMessage resposta) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => Task.FromResult(resposta);
    }
}
