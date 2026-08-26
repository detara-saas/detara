using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Detara.Application.Comunicacao;
using Detara.Domain.Notificacoes;
using Microsoft.Extensions.Options;

namespace Detara.Infrastructure.Notificacoes;

internal sealed class EmailClienteProvider(IProvedorEmail transporte) : IEmailClienteProvider
{
    public async Task<ResultadoEnvioComunicacaoCliente> EnviarAsync(
        MensagemEmailClienteProvider mensagem, CancellationToken cancellationToken)
    {
        var resultado = await transporte.EnviarAsync(new MensagemEmailProvedor(
            mensagem.Destinatario, mensagem.Assunto, mensagem.CorpoHtml,
            mensagem.ResponderPara, mensagem.ChaveIdempotencia), cancellationToken);
        return new(resultado.Sucesso, resultado.FalhaTemporaria,
            resultado.MensagemId, resultado.ErroSeguro);
    }
}

public sealed class WhatsAppGatewayOptions
{
    public const string Secao = "WhatsAppGateway";
    public bool Enabled { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
    public int TimeoutSeconds { get; init; } = 30;
}

internal sealed class WhatsAppGatewayClienteProvider(HttpClient httpClient,
    IOptions<WhatsAppGatewayOptions> options) : IWhatsAppClienteProvider
{
    private readonly WhatsAppGatewayOptions _options = options.Value;

    public Task<EstadoConexaoWhatsAppClienteProvider> IniciarConexaoAsync(
        Guid empresaId, CancellationToken cancellationToken) =>
        ConsultarConexaoAsync(HttpMethod.Post,
            $"sessions/{empresaId:D}/connect", empresaId, cancellationToken);

    public Task<EstadoConexaoWhatsAppClienteProvider> ObterStatusAsync(
        Guid empresaId, CancellationToken cancellationToken) =>
        ConsultarConexaoAsync(HttpMethod.Get,
            $"sessions/{empresaId:D}/status", empresaId, cancellationToken);

    public Task<EstadoConexaoWhatsAppClienteProvider> DesconectarAsync(
        Guid empresaId, CancellationToken cancellationToken) =>
        ConsultarConexaoAsync(HttpMethod.Delete,
            $"sessions/{empresaId:D}", empresaId, cancellationToken);

    public async Task<ResultadoEnvioComunicacaoCliente> EnviarAsync(
        MensagemWhatsAppClienteProvider mensagem, CancellationToken cancellationToken)
    {
        if (!TentarObterBaseUri(out var baseUri))
            return new(false, false, null,
                "O gateway WhatsApp não está configurado neste ambiente.");
        using var request = CriarRequest(HttpMethod.Post,
            new Uri(baseUri, "messages/send"), mensagem.EmpresaId);
        request.Content = JsonContent.Create(new
        {
            empresaId = mensagem.EmpresaId,
            telefone = mensagem.Destinatario,
            mensagem = mensagem.Mensagem,
            chaveIdempotencia = mensagem.ChaveIdempotencia
        });
        try
        {
            using var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return new(false, EhFalhaTemporaria(response.StatusCode), null,
                    await MapearErroSeguroAsync(response, cancellationToken));
            var payload = await response.Content.ReadFromJsonAsync<EnvioGatewayResponse>(
                cancellationToken: cancellationToken);
            if (payload is null || string.IsNullOrWhiteSpace(payload.MessageId))
                return new(false, false, null,
                    "O gateway WhatsApp retornou uma resposta inválida.");
            return new(true, false, payload.MessageId, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new(false, true, null,
                "O gateway WhatsApp não respondeu dentro do tempo esperado.");
        }
        catch (HttpRequestException)
        {
            return new(false, true, null,
                "O gateway WhatsApp não está disponível no momento.");
        }
    }

    private async Task<EstadoConexaoWhatsAppClienteProvider> ConsultarConexaoAsync(
        HttpMethod method, string relativePath, Guid empresaId,
        CancellationToken cancellationToken)
    {
        if (!TentarObterBaseUri(out var baseUri))
            return Erro("O gateway WhatsApp não está configurado neste ambiente.");
        using var request = CriarRequest(method, new Uri(baseUri, relativePath), empresaId);
        try
        {
            using var response = await httpClient.SendAsync(request,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
                return Erro(await MapearErroSeguroAsync(response, cancellationToken));
            var payload = await response.Content.ReadFromJsonAsync<StatusGatewayResponse>(
                cancellationToken: cancellationToken);
            if (payload is null || !TentarMapearStatus(payload.Status, out var status))
                return Erro("O gateway WhatsApp retornou uma resposta inválida.");
            return new(status, ValidarQrCode(payload.QrCode),
                payload.UpdatedAt?.UtcDateTime, payload.LastConnectedAt?.UtcDateTime,
                ValidarNumero(payload.PhoneNumber), null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return Erro("O gateway WhatsApp não respondeu dentro do tempo esperado.");
        }
        catch (HttpRequestException)
        {
            return Erro("O gateway WhatsApp não está disponível no momento.");
        }
    }

    private HttpRequestMessage CriarRequest(HttpMethod method, Uri uri, Guid empresaId)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        request.Headers.Add("X-Detara-Tenant-Id", empresaId.ToString("D"));
        return request;
    }

    private bool TentarObterBaseUri(out Uri baseUri)
    {
        baseUri = null!;
        if (!_options.Enabled || _options.ApiKey?.Length < 32 ||
            !Uri.TryCreate(_options.BaseUrl, UriKind.Absolute, out var configurada) ||
            configurada.Scheme != Uri.UriSchemeHttp &&
            configurada.Scheme != Uri.UriSchemeHttps) return false;
        baseUri = configurada;
        return true;
    }

    private static bool TentarMapearStatus(string? status,
        out StatusSessaoWhatsApp resultado)
    {
        resultado = status switch
        {
            "Disconnected" => StatusSessaoWhatsApp.Desconectada,
            "Connecting" or "WaitingQRCode" => StatusSessaoWhatsApp.Conectando,
            "Connected" => StatusSessaoWhatsApp.Conectada,
            "Error" => StatusSessaoWhatsApp.Erro,
            "Reconnecting" => StatusSessaoWhatsApp.Reconectando,
            _ => (StatusSessaoWhatsApp)(-1)
        };
        return Enum.IsDefined(resultado);
    }

    private static string? ValidarNumero(string? numero)
    {
        if (string.IsNullOrWhiteSpace(numero)) return null;
        var digitos = new string(numero.Where(char.IsDigit).ToArray());
        return digitos.Length is >= 8 and <= 15 ? digitos : null;
    }

    private static string? ValidarQrCode(string? qrCode)
    {
        const string prefixo = "data:image/png;base64,";
        if (string.IsNullOrWhiteSpace(qrCode) || qrCode.Length > 512 * 1024 ||
            !qrCode.StartsWith(prefixo, StringComparison.Ordinal)) return null;
        try
        {
            var bytes = Convert.FromBase64String(qrCode[prefixo.Length..]);
            return bytes.Length is > 0 and <= 384 * 1024 ? qrCode : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }

    private static bool EhFalhaTemporaria(HttpStatusCode status) =>
        (int)status >= 500 || status is HttpStatusCode.RequestTimeout or
            HttpStatusCode.TooManyRequests;

    private static async Task<string> MapearErroSeguroAsync(HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            var erro = await response.Content.ReadFromJsonAsync<ErroGatewayResponse>(
                cancellationToken: cancellationToken);
            return erro?.Code switch
            {
                "whatsapp_nao_conectado" => "O WhatsApp da empresa não está conectado.",
                "telefone_nao_registrado" => "O telefone do cliente não possui uma conta WhatsApp válida.",
                "envio_estado_incerto" => "Não foi possível confirmar o envio; ele não será repetido automaticamente.",
                "tenant_invalido" => "O gateway recusou o tenant da comunicação.",
                "nao_autorizado" => "A autenticação interna do gateway WhatsApp foi recusada.",
                "requisicao_invalida" => "O gateway recusou os dados da comunicação.",
                _ => "O gateway WhatsApp não conseguiu concluir a operação."
            };
        }
        catch (Exception exception) when (exception is HttpRequestException or
            NotSupportedException or System.Text.Json.JsonException)
        {
            return "O gateway WhatsApp não conseguiu concluir a operação.";
        }
    }

    private static EstadoConexaoWhatsAppClienteProvider Erro(string mensagem) =>
        new(StatusSessaoWhatsApp.Erro, null, null, null, null, mensagem);

    private sealed record StatusGatewayResponse(string? Status, string? QrCode,
        DateTimeOffset? UpdatedAt, DateTimeOffset? LastConnectedAt,
        string? PhoneNumber);
    private sealed record EnvioGatewayResponse(string? Status, string? MessageId,
        DateTimeOffset? SentAt, bool Reused);
    private sealed record ErroGatewayResponse(string? Code);
}
