using Detara.Domain.Entidades;

namespace Detara.Domain.Notificacoes;

public sealed class ConfiguracaoNotificacaoEmpresa : EntidadeEmpresaBase
{
    private ConfiguracaoNotificacaoEmpresa() { }

    public ConfiguracaoNotificacaoEmpresa(Guid empresaId, bool enviarVeiculoProntoAutomaticamente,
        string? responderParaEmail, Guid usuarioId) : base(Guid.NewGuid(), empresaId) =>
        Atualizar(enviarVeiculoProntoAutomaticamente, responderParaEmail, usuarioId);

    public bool EnviarVeiculoProntoAutomaticamente { get; private set; }
    public string? ResponderParaEmail { get; private set; }
    public Guid AtualizadoPorUsuarioId { get; private set; }
    public long Versao { get; private set; } = 1;

    public void Atualizar(bool enviarAutomaticamente, string? responderParaEmail, Guid usuarioId)
    {
        if (usuarioId == Guid.Empty) throw new ArgumentException("O usuário deve ser informado.", nameof(usuarioId));
        var email = string.IsNullOrWhiteSpace(responderParaEmail) ? null : responderParaEmail.Trim().ToLowerInvariant();
        if (email?.Length > 200) throw new ArgumentException("O e-mail de resposta deve possuir no máximo 200 caracteres.", nameof(responderParaEmail));
        EnviarVeiculoProntoAutomaticamente = enviarAutomaticamente;
        ResponderParaEmail = email;
        AtualizadoPorUsuarioId = usuarioId;
        Versao++;
        MarcarComoAtualizada();
    }
}
