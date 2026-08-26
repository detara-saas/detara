using Detara.Domain.Entidades;

namespace Detara.Domain.Notificacoes;

public sealed class SessaoWhatsAppEmpresa : EntidadeEmpresaBase
{
    private SessaoWhatsAppEmpresa() { }

    public SessaoWhatsAppEmpresa(Guid empresaId, string sessionKey)
        : base(Guid.NewGuid(), empresaId)
    {
        SessionKey = NormalizarSessionKey(sessionKey);
        Status = StatusSessaoWhatsApp.Desconectada;
    }

    public string SessionKey { get; private set; } = string.Empty;
    public StatusSessaoWhatsApp Status { get; private set; }
    public DateTime? UltimaConexaoEmUtc { get; private set; }
    public string? UltimoErroSeguro { get; private set; }
    public long Versao { get; private set; } = 1;

    public void AtualizarStatus(StatusSessaoWhatsApp status,
        DateTime? ultimaConexaoEmUtc, string? erroSeguro = null)
    {
        if (!Enum.IsDefined(status))
            throw new ArgumentException("O status da sessão WhatsApp é inválido.", nameof(status));
        var novaUltimaConexao = ultimaConexaoEmUtc ?? UltimaConexaoEmUtc;
        var novoErro = status == StatusSessaoWhatsApp.Erro
            ? NormalizarErro(erroSeguro)
            : null;
        if (Status == status && UltimaConexaoEmUtc == novaUltimaConexao &&
            UltimoErroSeguro == novoErro)
            return;
        Status = status;
        UltimaConexaoEmUtc = novaUltimaConexao;
        UltimoErroSeguro = novoErro;
        Versao++;
        MarcarComoAtualizada();
    }

    private static string NormalizarSessionKey(string sessionKey)
    {
        var normalizada = sessionKey?.Trim() ?? string.Empty;
        if (normalizada.Length is < 8 or > 80 ||
            normalizada.Any(caractere => !char.IsLetterOrDigit(caractere) && caractere != '-'))
            throw new ArgumentException("A chave da sessão WhatsApp é inválida.", nameof(sessionKey));
        return normalizada;
    }

    private static string NormalizarErro(string? erroSeguro)
    {
        if (string.IsNullOrWhiteSpace(erroSeguro))
            return "Não foi possível consultar a sessão WhatsApp.";
        var normalizado = erroSeguro.Trim();
        return normalizado.Length <= 500 ? normalizado : normalizado[..500];
    }
}
