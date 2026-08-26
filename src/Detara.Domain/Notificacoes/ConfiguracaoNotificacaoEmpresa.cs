using Detara.Domain.Entidades;

namespace Detara.Domain.Notificacoes;

public sealed class ConfiguracaoNotificacaoEmpresa : EntidadeEmpresaBase
{
    private ConfiguracaoNotificacaoEmpresa() { }

    public ConfiguracaoNotificacaoEmpresa(Guid empresaId, CanalComunicacaoVeiculoPronto canalAutomaticoVeiculoPronto,
        string? responderParaEmail, Guid usuarioId) : base(Guid.NewGuid(), empresaId) =>
        Atualizar(canalAutomaticoVeiculoPronto, false, responderParaEmail, usuarioId);

    public ConfiguracaoNotificacaoEmpresa(Guid empresaId,
        CanalComunicacaoVeiculoPronto canalAutomaticoVeiculoPronto,
        bool permitirComunicacaoWhatsApp, string? responderParaEmail,
        Guid usuarioId) : base(Guid.NewGuid(), empresaId) =>
        Atualizar(canalAutomaticoVeiculoPronto, permitirComunicacaoWhatsApp,
            responderParaEmail, usuarioId);

    public CanalComunicacaoVeiculoPronto CanalAutomaticoVeiculoPronto { get; private set; }
    public string? ResponderParaEmail { get; private set; }
    public bool PermitirComunicacaoWhatsApp { get; private set; }
    public DateTime? DataAtivacaoWhatsAppEmUtc { get; private set; }
    public Guid? UsuarioAtivacaoWhatsAppId { get; private set; }
    public Guid AtualizadoPorUsuarioId { get; private set; }
    public long Versao { get; private set; } = 1;

    public void Atualizar(CanalComunicacaoVeiculoPronto canalAutomaticoVeiculoPronto,
        string? responderParaEmail, Guid usuarioId) =>
        Atualizar(canalAutomaticoVeiculoPronto, PermitirComunicacaoWhatsApp,
            responderParaEmail, usuarioId);

    public void Atualizar(CanalComunicacaoVeiculoPronto canalAutomaticoVeiculoPronto,
        bool permitirComunicacaoWhatsApp, string? responderParaEmail, Guid usuarioId)
    {
        if (usuarioId == Guid.Empty) throw new ArgumentException("O usuário deve ser informado.", nameof(usuarioId));
        if (!Enum.IsDefined(canalAutomaticoVeiculoPronto))
            throw new ArgumentException("O canal automático é inválido.", nameof(canalAutomaticoVeiculoPronto));
        var email = string.IsNullOrWhiteSpace(responderParaEmail) ? null : responderParaEmail.Trim().ToLowerInvariant();
        if (email?.Length > 200) throw new ArgumentException("O e-mail de resposta deve possuir no máximo 200 caracteres.", nameof(responderParaEmail));
        CanalAutomaticoVeiculoPronto = canalAutomaticoVeiculoPronto;
        ResponderParaEmail = email;
        if (permitirComunicacaoWhatsApp && !PermitirComunicacaoWhatsApp)
        {
            DataAtivacaoWhatsAppEmUtc = DateTime.UtcNow;
            UsuarioAtivacaoWhatsAppId = usuarioId;
        }
        PermitirComunicacaoWhatsApp = permitirComunicacaoWhatsApp;
        AtualizadoPorUsuarioId = usuarioId;
        Versao++;
        MarcarComoAtualizada();
    }
}
