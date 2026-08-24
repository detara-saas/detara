namespace Detara.Contracts.Notificacoes;

public enum OrigemTemplateEmailContrato { PadraoDetara = 1, PersonalizadoEmpresa = 2 }
public enum StatusNotificacaoEmailContrato { Pendente = 1, Processando = 2, Enviada = 3, Falhou = 4, SemDestinatario = 5 }
public enum ResultadoTentativaNotificacaoEmailContrato { Enviada = 1, FalhaTemporaria = 2, FalhaTerminal = 3 }
public enum TipoTentativaNotificacaoEmailContrato { Automatica = 1, Manual = 2 }

public sealed record ConfiguracaoNotificacaoResponse(bool EnviarVeiculoProntoAutomaticamente,
    string? ResponderParaEmail, DateTime? AtualizadoEmUtc);
public sealed record AtualizarConfiguracaoNotificacaoRequest(bool EnviarVeiculoProntoAutomaticamente,
    string? ResponderParaEmail);
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
    bool EnviarVeiculoProntoAutomaticamente, string? EmailDestinoAtual);
public sealed record ReenviarAvisoVeiculoProntoRequest(Guid SolicitacaoId);
