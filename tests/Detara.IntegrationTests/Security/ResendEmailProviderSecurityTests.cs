using System.Net;
using Detara.Application.Notificacoes;
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
