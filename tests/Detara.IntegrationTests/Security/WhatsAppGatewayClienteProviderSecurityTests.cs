using System.Net;
using System.Text.Json;
using Detara.Application.Comunicacao;
using Detara.Domain.Notificacoes;
using Detara.Infrastructure.Notificacoes;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Security;

public sealed class WhatsAppGatewayClienteProviderSecurityTests
{
    private const string ApiKey = "chave-interna-whatsapp-para-testes-123456";

    [Fact]
    public async Task Envio_UsaAutenticacaoETenantDoComandoEmCabecalhoEPayload()
    {
        var empresaId = Guid.NewGuid();
        RequisicaoCapturada? capturada = null;
        using var http = new HttpClient(new HandlerFixo(async (request, cancellationToken) =>
        {
            capturada = new(request.Method, request.RequestUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.GetValues("X-Detara-Tenant-Id").Single(),
                await request.Content!.ReadAsStringAsync(cancellationToken));
            return Json(HttpStatusCode.OK,
                """{"status":"Sent","messageId":"wamid-teste","sentAt":"2026-08-25T22:00:00Z","reused":false}""");
        }));
        var provider = CriarProvider(http);

        var resultado = await provider.EnviarAsync(new(
            empresaId, "11999998888", "Mensagem transacional", "comunicacao/12345678"),
            CancellationToken.None);

        Assert.True(resultado.Sucesso);
        Assert.Equal("wamid-teste", resultado.MensagemId);
        Assert.NotNull(capturada);
        Assert.Equal(HttpMethod.Post, capturada.Method);
        Assert.Equal("https://gateway.detara.test/messages/send", capturada.Uri?.ToString());
        Assert.Equal("Bearer", capturada.AuthenticationScheme);
        Assert.Equal(ApiKey, capturada.AuthenticationParameter);
        Assert.Equal(empresaId.ToString("D"), capturada.TenantHeader);
        using var payload = JsonDocument.Parse(capturada.Body!);
        Assert.Equal(empresaId, payload.RootElement.GetProperty("empresaId").GetGuid());
        Assert.Equal("11999998888", payload.RootElement.GetProperty("telefone").GetString());
        Assert.Equal("Mensagem transacional", payload.RootElement.GetProperty("mensagem").GetString());
    }

    [Fact]
    public async Task Conexao_RetornaSomenteEstadoEQRCodeValidadoDoTenantSolicitado()
    {
        var empresaId = Guid.NewGuid();
        var qrCode = "data:image/png;base64," + Convert.ToBase64String([1, 2, 3, 4]);
        RequisicaoCapturada? capturada = null;
        using var http = new HttpClient(new HandlerFixo((request, _) =>
        {
            capturada = new(request.Method, request.RequestUri,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                request.Headers.GetValues("X-Detara-Tenant-Id").Single(), null);
            return Task.FromResult(Json(HttpStatusCode.OK,
                $$"""{"status":"WaitingQRCode","qrCode":"{{qrCode}}","updatedAt":"2026-08-25T22:00:00Z","lastConnectedAt":null}"""));
        }));
        var provider = CriarProvider(http);

        var resultado = await provider.IniciarConexaoAsync(empresaId, CancellationToken.None);

        Assert.Equal(StatusSessaoWhatsApp.AguardandoQrCode, resultado.Status);
        Assert.Equal(qrCode, resultado.QrCodeDataUrl);
        Assert.Equal(HttpMethod.Post, capturada?.Method);
        Assert.Equal($"https://gateway.detara.test/sessions/{empresaId:D}/connect",
            capturada?.Uri?.ToString());
        Assert.Equal(empresaId.ToString("D"), capturada?.TenantHeader);
    }

    [Fact]
    public async Task GatewaySemSessao_RetornaErroSeguroSemVazarResposta()
    {
        using var http = new HttpClient(new HandlerFixo((_, _) => Task.FromResult(
            Json(HttpStatusCode.Conflict,
                """{"code":"whatsapp_nao_conectado","detail":"session-token-secreto"}"""))));
        var provider = CriarProvider(http);

        var resultado = await provider.EnviarAsync(new(
            Guid.NewGuid(), "11999998888", "Mensagem", "comunicacao/87654321"),
            CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.False(resultado.FalhaTemporaria);
        Assert.Equal("O WhatsApp da empresa não está conectado.", resultado.ErroSeguro);
        Assert.DoesNotContain("secreto", resultado.ErroSeguro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task QrCodeInvalido_NaoEhExpostoAoFrontend()
    {
        using var http = new HttpClient(new HandlerFixo((_, _) => Task.FromResult(
            Json(HttpStatusCode.OK,
                """{"status":"WaitingQRCode","qrCode":"data:text/html;base64,c2VncmVkbw=="}"""))));
        var provider = CriarProvider(http);

        var resultado = await provider.ObterStatusAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(StatusSessaoWhatsApp.AguardandoQrCode, resultado.Status);
        Assert.Null(resultado.QrCodeDataUrl);
    }

    private static WhatsAppGatewayClienteProvider CriarProvider(HttpClient http) => new(
        http,
        Options.Create(new WhatsAppGatewayOptions
        {
            Enabled = true,
            BaseUrl = "https://gateway.detara.test/",
            ApiKey = ApiKey,
            TimeoutSeconds = 30
        }));

    private static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };

    private sealed record RequisicaoCapturada(HttpMethod Method, Uri? Uri,
        string? AuthenticationScheme, string? AuthenticationParameter,
        string TenantHeader, string? Body);

    private sealed class HandlerFixo(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request, cancellationToken);
    }
}
