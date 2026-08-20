namespace Detara.Application.Comunicacao;

public sealed record MensagemEmailProvedor(
    string Destinatario,
    string Assunto,
    string CorpoHtml,
    string? ResponderPara,
    string ChaveIdempotencia);

public sealed record ResultadoEnvioEmail(
    bool Sucesso,
    bool FalhaTemporaria,
    string? MensagemId,
    string? ErroSeguro);

public interface IProvedorEmail
{
    Task<ResultadoEnvioEmail> EnviarAsync(
        MensagemEmailProvedor mensagem,
        CancellationToken cancellationToken);
}
