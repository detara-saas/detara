using System.Net;
using System.Text.Json;
using Detara.Application.Notificacoes;
using Detara.Application.Comunicacao;
using Detara.Infrastructure.Notificacoes;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Security;

public sealed class ResendEmailProviderSecurityTests
{
    [Fact]
    public async Task TimeoutDoProvedor_RetornaFalhaTemporariaSegura()
    {
        using var http = new HttpClient(new HandlerFixo((_, _) =>
            throw new TaskCanceledException("detalhe interno")))
        {
            BaseAddress = new Uri("https://api.resend.com/")
        };
        var provider = CriarProvider(http);

        var resultado = await provider.EnviarAsync(CriarMensagem(), CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.True(resultado.FalhaTemporaria);
        Assert.DoesNotContain("interno", resultado.ErroSeguro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task JsonInvalidoDoProvedor_NaoVazaRespostaExterna()
    {
        using var http = new HttpClient(new HandlerFixo((_, _) => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{ resposta-invalida: segredo-do-provedor }")
            })))
        {
            BaseAddress = new Uri("https://api.resend.com/")
        };
        var provider = CriarProvider(http);

        var resultado = await provider.EnviarAsync(CriarMensagem(), CancellationToken.None);

        Assert.False(resultado.Sucesso);
        Assert.True(resultado.FalhaTemporaria);
        Assert.DoesNotContain("segredo", resultado.ErroSeguro, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task LogoInline_EhEnviadaComoCidSemDataUriNoHtml()
    {
        string? json = null;
        using var http = new HttpClient(new HandlerFixo(async (request, ct) =>
        {
            json = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{\"id\":\"email-1\"}") };
        }))
        { BaseAddress = new Uri("https://api.resend.com/") };
        var provider = CriarProvider(http);
        var mensagem = CriarMensagem() with
        {
            CorpoHtml = "<img src=\"cid:company-logo\" alt=\"Logo da empresa\">",
            AnexoInline = new("company-logo.png", "image/png", "company-logo", [1, 2, 3])
        };

        var resultado = await provider.EnviarAsync(mensagem, CancellationToken.None);

        Assert.True(resultado.Sucesso);
        using var documento = JsonDocument.Parse(json!);
        var anexo = Assert.Single(documento.RootElement.GetProperty("attachments").EnumerateArray());
        Assert.Equal("company-logo", anexo.GetProperty("content_id").GetString());
        Assert.Equal(Convert.ToBase64String([1, 2, 3]), anexo.GetProperty("content").GetString());
        Assert.DoesNotContain("data:image", json, StringComparison.OrdinalIgnoreCase);
    }

    private static ResendEmailProvider CriarProvider(HttpClient http) => new(
        http,
        Options.Create(new EmailOptions
        {
            Provider = "Resend",
            ApiKey = "chave-apenas-de-teste",
            FromAddress = "nao-responda@detara.local",
            FromName = "Detara"
        }));

    private static MensagemEmailProvedor CriarMensagem() => new(
        "destino@detara.local",
        "Assunto",
        "<p>Corpo</p>",
        null,
        "teste-idempotencia");

    private sealed class HandlerFixo(
        Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> responder)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => responder(request, cancellationToken);
    }
}
