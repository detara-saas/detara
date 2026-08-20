using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Text.Json;
using Detara.Application.Notificacoes;
using Detara.Application.Comunicacao;
using Microsoft.Extensions.Options;

namespace Detara.Infrastructure.Notificacoes;

public sealed class EmailOptions
{
    public const string Secao = "Email";
    public string Provider { get; init; } = "Resend";
    public string? ApiKey { get; init; }
    public string FromAddress { get; init; } = "";
    public string FromName { get; init; } = "Detara";
}

internal sealed class ResendEmailProvider(HttpClient http, IOptions<EmailOptions> options) : IProvedorEmail
{
    public async Task<ResultadoEnvioEmail> EnviarAsync(MensagemEmailProvedor mensagem, CancellationToken ct)
    {
        var config = options.Value;
        if (!string.Equals(config.Provider, "Resend", StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(config.ApiKey) || string.IsNullOrWhiteSpace(config.FromAddress))
            return new(false, false, null, "Envio de e-mail não configurado neste ambiente.");
        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.ApiKey);
        request.Headers.Add("Idempotency-Key", mensagem.ChaveIdempotencia);
        request.Content = JsonContent.Create(new ResendRequest($"{config.FromName} <{config.FromAddress}>",
            [mensagem.Destinatario], mensagem.Assunto, mensagem.CorpoHtml, mensagem.ResponderPara));
        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode)
            {
                var payload = await response.Content.ReadFromJsonAsync<ResendResponse>(cancellationToken: ct);
                return new(true, false, payload?.Id ?? "aceita-sem-id", null);
            }
            var temporaria = response.StatusCode is HttpStatusCode.RequestTimeout or HttpStatusCode.Conflict or HttpStatusCode.TooManyRequests || (int)response.StatusCode >= 500;
            return new(false, temporaria, null, temporaria ? "Falha temporária no provedor de e-mail." : "O provedor rejeitou a mensagem.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return new(false, true, null, "O provedor de e-mail excedeu o tempo de resposta.");
        }
        catch (HttpRequestException)
        {
            return new(false, true, null, "Não foi possível acessar o provedor de e-mail.");
        }
        catch (JsonException)
        {
            return new(false, true, null, "O provedor de e-mail retornou uma resposta inválida.");
        }
    }
    private sealed record ResendRequest([property: JsonPropertyName("from")] string From,
        [property: JsonPropertyName("to")] string[] To, [property: JsonPropertyName("subject")] string Subject,
        [property: JsonPropertyName("html")] string Html, [property: JsonPropertyName("reply_to"),
        JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ReplyTo);
    private sealed record ResendResponse([property: JsonPropertyName("id")] string Id);
}
