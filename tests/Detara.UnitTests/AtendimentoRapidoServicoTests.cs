using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Comum;
using Detara.Contracts.Financeiro;
using Detara.Web.Servicos;

namespace Detara.UnitTests;

public sealed class AtendimentoRapidoServicoTests
{
    [Fact]
    public async Task FalhaDeRede_PreservaSolicitacaoParaConfirmacaoSegura()
    {
        using var http = new HttpClient(new Handler(_ => throw new HttpRequestException())) { BaseAddress = new("http://localhost") };
        var resultado = await new OrdensServicoServico(http).RegistrarRapidoAsync(Request());
        Assert.False(resultado.Sucesso);
        Assert.True(resultado.ConfirmacaoIncerta);
    }

    [Fact]
    public async Task ValidacaoDoServidor_PermiteCorrigirCamposEExibeMensagemEspecifica()
    {
        using var http = new HttpClient(new Handler(_ => new(HttpStatusCode.BadRequest)
        {
            Content = JsonContent.Create(RespostaApi<AtendimentoRapidoResponse>.Falha("Verifique os dados.", "validacao",
                new Dictionary<string, string[]> { ["ValorPago"] = ["Informe um valor válido."] }))
        }))
        { BaseAddress = new("http://localhost") };
        var resultado = await new OrdensServicoServico(http).RegistrarRapidoAsync(Request());
        Assert.False(resultado.Sucesso);
        Assert.False(resultado.ConfirmacaoIncerta);
        Assert.Equal("Informe um valor válido.", resultado.Mensagem);
    }

    [Fact]
    public async Task RespostaPerdida_ReenviaExatamenteAMesmaSolicitacao()
    {
        var corpos = new List<string>();
        var response = new AtendimentoRapidoResponse(Guid.NewGuid(), "OS-TESTE", Guid.NewGuid());
        using var http = new HttpClient(new HandlerAsync(async request =>
        {
            corpos.Add(await request.Content!.ReadAsStringAsync());
            if (corpos.Count == 1) throw new HttpRequestException();
            return new(HttpStatusCode.OK) { Content = JsonContent.Create(RespostaApi<AtendimentoRapidoResponse>.Ok(response)) };
        }))
        { BaseAddress = new("http://localhost") };
        var api = new OrdensServicoServico(http);
        var lancamento = Request();
        Assert.True((await api.RegistrarRapidoAsync(lancamento)).ConfirmacaoIncerta);
        var confirmado = await api.RegistrarRapidoAsync(lancamento);
        Assert.True(confirmado.Sucesso);
        Assert.Equal(response, confirmado.Resultado);
        Assert.Equal(corpos[0], corpos[1]);
    }

    private static RegistrarAtendimentoRapidoRequest Request() => new(Guid.NewGuid(), Guid.NewGuid(),
        Guid.NewGuid(), TipoItemOrcamentoContrato.Servico, Guid.NewGuid(), 150, FormaPagamentoContrato.Pix,
        DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1), DateTimeKind.Unspecified));

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(response(request));
    }

    private sealed class HandlerAsync(Func<HttpRequestMessage, Task<HttpResponseMessage>> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => response(request);
    }
}
