using Detara.Domain.Notificacoes;

namespace Detara.Application.Comunicacao;

public sealed record MensagemEmailClienteProvider(
    string Destinatario,
    string Assunto,
    string CorpoHtml,
    string? ResponderPara,
    string ChaveIdempotencia);

public sealed record MensagemWhatsAppClienteProvider(
    Guid EmpresaId,
    string Destinatario,
    string Mensagem,
    string ChaveIdempotencia);

public sealed record EstadoConexaoWhatsAppClienteProvider(
    StatusSessaoWhatsApp Status,
    string? QrCodeDataUrl,
    DateTime? AtualizadoEmUtc,
    DateTime? UltimaConexaoEmUtc,
    string? NumeroConectado,
    string? ErroSeguro);

public sealed record ResultadoEnvioComunicacaoCliente(
    bool Sucesso,
    bool FalhaTemporaria,
    string? MensagemId,
    string? ErroSeguro);

public interface IEmailClienteProvider
{
    Task<ResultadoEnvioComunicacaoCliente> EnviarAsync(
        MensagemEmailClienteProvider mensagem,
        CancellationToken cancellationToken);
}

public interface IWhatsAppClienteProvider
{
    Task<EstadoConexaoWhatsAppClienteProvider> IniciarConexaoAsync(
        Guid empresaId,
        CancellationToken cancellationToken);
    Task<EstadoConexaoWhatsAppClienteProvider> ObterStatusAsync(
        Guid empresaId,
        CancellationToken cancellationToken);
    Task<EstadoConexaoWhatsAppClienteProvider> DesconectarAsync(
        Guid empresaId,
        CancellationToken cancellationToken);
    Task<ResultadoEnvioComunicacaoCliente> EnviarAsync(
        MensagemWhatsAppClienteProvider mensagem,
        CancellationToken cancellationToken);
}
