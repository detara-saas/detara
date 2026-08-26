namespace Detara.Contracts.Notificacoes;

public enum OrigemTemplateEmailContrato { PadraoDetara = 1, PersonalizadoEmpresa = 2 }
public enum StatusNotificacaoEmailContrato { Pendente = 1, Processando = 2, Enviada = 3, Falhou = 4, SemDestinatario = 5 }
public enum ResultadoTentativaNotificacaoEmailContrato { Enviada = 1, FalhaTemporaria = 2, FalhaTerminal = 3 }
public enum TipoTentativaNotificacaoEmailContrato { Automatica = 1, Manual = 2 }
public enum CanalComunicacaoVeiculoProntoContrato { Nenhum = 0, Email = 1, WhatsApp = 2 }
public enum CanalComunicacaoClienteContrato { Email = 1, WhatsApp = 2 }
public enum TipoComunicacaoClienteContrato { VeiculoPronto = 1, TesteWhatsApp = 2 }
public enum StatusComunicacaoClienteContrato { Pendente = 1, Enviado = 2, Falhou = 3 }
public enum OrigemComunicacaoClienteContrato { Automatica = 1, Manual = 2 }
public enum StatusSessaoWhatsAppContrato
{
    Desconectada = 0,
    AguardandoQrCode = 1,
    Conectada = 2,
    Erro = 3,
    Conectando = 4,
    Reconectando = 5
}

public sealed record ConfiguracaoNotificacaoResponse(CanalComunicacaoVeiculoProntoContrato CanalAutomaticoVeiculoPronto,
    string? ResponderParaEmail, bool PermitirComunicacaoWhatsApp,
    DateTime? DataAtivacaoWhatsAppEmUtc, string? UsuarioAtivacaoWhatsApp,
    DateTime? AtualizadoEmUtc)
{
    public bool EnviarVeiculoProntoAutomaticamente =>
        CanalAutomaticoVeiculoPronto != CanalComunicacaoVeiculoProntoContrato.Nenhum;
}
public sealed record AtualizarConfiguracaoNotificacaoRequest(
    CanalComunicacaoVeiculoProntoContrato CanalAutomaticoVeiculoPronto,
    string? ResponderParaEmail, bool PermitirComunicacaoWhatsApp);
public sealed record TemplateEmailResponse(string Assunto, string CorpoHtml, OrigemTemplateEmailContrato Origem,
    DateTime? AtualizadoEmUtc);
public sealed record SalvarTemplateEmailRequest(string Assunto, string CorpoHtml);
public sealed record PreviewTemplateEmailRequest(string Assunto, string CorpoHtml);
public sealed record PreviewTemplateEmailResponse(string Assunto, string CorpoHtmlCompleto);
public sealed record TentativaNotificacaoEmailResponse(int Numero, TipoTentativaNotificacaoEmailContrato Tipo,
    DateTime ConcluidaEmUtc, ResultadoTentativaNotificacaoEmailContrato Resultado, string? ErroSeguro);
public sealed record NotificacaoEmailResponse(Guid Id, Guid OrdemServicoId, StatusNotificacaoEmailContrato Status,
    string? DestinatarioEmail, string DestinatarioNome, OrigemTemplateEmailContrato OrigemTemplate,
    int QuantidadeTentativas, DateTime CriadoEmUtc, DateTime? EnviadaEmUtc, string? UltimoErroSeguro,
    IReadOnlyCollection<TentativaNotificacaoEmailResponse> Tentativas);
public sealed record NotificacaoOrdemServicoResponse(bool Existe, NotificacaoEmailResponse? Notificacao,
    CanalComunicacaoVeiculoProntoContrato CanalAutomaticoVeiculoPronto,
    string? EmailDestinoAtual, string? WhatsAppDestinoAtual,
    IReadOnlyCollection<ComunicacaoClienteResponse> Comunicacoes)
{
    public bool EnviarVeiculoProntoAutomaticamente =>
        CanalAutomaticoVeiculoPronto != CanalComunicacaoVeiculoProntoContrato.Nenhum;
}
public sealed record ReenviarAvisoVeiculoProntoRequest(Guid SolicitacaoId);
public sealed record ComunicarClienteVeiculoProntoRequest(CanalComunicacaoClienteContrato Canal,
    Guid SolicitacaoId);
public sealed record ComunicacaoClienteResponse(Guid Id, Guid? OrdemServicoId,
    CanalComunicacaoClienteContrato Canal, TipoComunicacaoClienteContrato Tipo,
    StatusComunicacaoClienteContrato Status, OrigemComunicacaoClienteContrato Origem,
    string? Destinatario, string Mensagem, string? SolicitadoPorUsuarioNome,
    DateTime CriadoEmUtc, DateTime? DataEnvioUtc, string? UltimoErroSeguro);
public sealed record SessaoWhatsAppResponse(StatusSessaoWhatsAppContrato Status,
    string? QrCodeDataUrl, DateTime? AtualizadoEmUtc,
    DateTime? UltimaConexaoEmUtc, string? NumeroConectado,
    string? UltimoErroSeguro);
public sealed record EnviarTesteWhatsAppRequest(string Numero, bool Confirmado,
    Guid SolicitacaoId);
