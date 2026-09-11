using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Notificacoes;
using Detara.Web.Servicos;

namespace Detara.UnitTests;

public sealed class NotificacoesServicoTests
{
    [Fact]
    public async Task AguardarComunicacaoFinal_ReconsultaAteBackendConfirmarEnvio()
    {
        var comunicacaoId = Guid.NewGuid();
        var handler = new RespostasHandler(
            Resposta(comunicacaoId, StatusComunicacaoClienteContrato.Pendente),
            Resposta(comunicacaoId, StatusComunicacaoClienteContrato.Enviado));
        var servico = new NotificacoesServico(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://detara.test/")
        });

        var resultado = await servico.AguardarComunicacaoFinalAsync(
            Guid.NewGuid(), comunicacaoId, maximoConsultas: 2,
            intervalo: TimeSpan.FromMilliseconds(1));

        Assert.True(resultado.Sucesso);
        Assert.Equal(2, handler.QuantidadeChamadas);
        Assert.Equal(StatusComunicacaoClienteContrato.Enviado,
            Assert.Single(resultado.Resultado!.Comunicacoes).Status);
    }

    [Fact]
    public async Task AguardarComunicacaoFinal_EhLimitadoQuandoProcessamentoContinuaPendente()
    {
        var comunicacaoId = Guid.NewGuid();
        var handler = new RespostasHandler(
            Resposta(comunicacaoId, StatusComunicacaoClienteContrato.Pendente),
            Resposta(comunicacaoId, StatusComunicacaoClienteContrato.Pendente));
        var servico = new NotificacoesServico(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://detara.test/")
        });

        var resultado = await servico.AguardarComunicacaoFinalAsync(
            Guid.NewGuid(), comunicacaoId, maximoConsultas: 2,
            intervalo: TimeSpan.FromMilliseconds(1));

        Assert.True(resultado.Sucesso);
        Assert.Equal(2, handler.QuantidadeChamadas);
        Assert.Equal(StatusComunicacaoClienteContrato.Pendente,
            Assert.Single(resultado.Resultado!.Comunicacoes).Status);
    }

    private static HttpResponseMessage Resposta(
        Guid comunicacaoId,
        StatusComunicacaoClienteContrato status)
    {
        var comunicacao = new ComunicacaoClienteResponse(
            comunicacaoId, Guid.NewGuid(), CanalComunicacaoClienteContrato.Email,
            TipoComunicacaoClienteContrato.VeiculoPronto, status,
            OrigemComunicacaoClienteContrato.Manual, "cliente@example.com", "Mensagem",
            "Veículo pronto", "Operador", DateTime.UtcNow,
            status == StatusComunicacaoClienteContrato.Enviado ? DateTime.UtcNow : null, null);
        var notificacao = new NotificacaoOrdemServicoResponse(
            true, null, CanalComunicacaoVeiculoProntoContrato.Nenhum,
            "cliente@example.com", null, [comunicacao]);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(RespostaApi<NotificacaoOrdemServicoResponse>.Ok(notificacao))
        };
    }

    private sealed class RespostasHandler(params HttpResponseMessage[] respostas) : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _respostas = new(respostas);
        public int QuantidadeChamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            QuantidadeChamadas++;
            return Task.FromResult(_respostas.Dequeue());
        }
    }
}
