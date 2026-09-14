namespace Detara.Application.Comunicacao;

public sealed record MensagemEmailProvedor(
    string Destinatario,
    string Assunto,
    string CorpoHtml,
    string? ResponderPara,
    string ChaveIdempotencia,
    AnexoEmailInline? AnexoInline = null);

public sealed record AnexoEmailInline(string NomeArquivo, string ContentType, string ContentId, byte[] Conteudo);

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
