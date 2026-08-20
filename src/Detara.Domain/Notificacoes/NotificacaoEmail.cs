using Detara.Domain.Entidades;

namespace Detara.Domain.Notificacoes;

public sealed class NotificacaoEmail : EntidadeEmpresaBase
{
    private readonly List<TentativaNotificacaoEmail> _tentativas = [];
    private NotificacaoEmail() { }

    public NotificacaoEmail(Guid empresaId, Guid ordemServicoId, Guid clienteId, TipoTemplateEmail tipo,
        string? destinatarioEmail, string destinatarioNome, string assuntoSnapshot,
        string corpoHtmlSnapshot, OrigemTemplateEmail origemTemplate, string? responderParaSnapshot)
        : base(Guid.NewGuid(), empresaId)
    {
        if (ordemServicoId == Guid.Empty) throw new ArgumentException("A ordem de serviço deve ser informada.", nameof(ordemServicoId));
        OrdemServicoId = ordemServicoId;
        ClienteId = clienteId == Guid.Empty ? throw new ArgumentException("O cliente deve ser informado.", nameof(clienteId)) : clienteId;
        Tipo = tipo;
        DestinatarioNomeSnapshot = destinatarioNome.Trim();
        AssuntoSnapshot = assuntoSnapshot;
        CorpoHtmlSnapshot = corpoHtmlSnapshot;
        OrigemTemplate = origemTemplate;
        ResponderParaSnapshot = responderParaSnapshot;
        DestinatarioEmailSnapshot = NormalizarEmail(destinatarioEmail);
        Status = DestinatarioEmailSnapshot is null ? StatusNotificacaoEmail.SemDestinatario : StatusNotificacaoEmail.Pendente;
        ProximaTentativaEmUtc = Status == StatusNotificacaoEmail.Pendente ? DateTime.UtcNow : null;
        TipoProximaTentativa = TipoTentativaNotificacaoEmail.Automatica;
    }

    public Guid OrdemServicoId { get; private set; }
    public Guid ClienteId { get; private set; }
    public TipoTemplateEmail Tipo { get; private set; }
    public StatusNotificacaoEmail Status { get; private set; }
    public string? DestinatarioEmailSnapshot { get; private set; }
    public string DestinatarioNomeSnapshot { get; private set; } = string.Empty;
    public string AssuntoSnapshot { get; private set; } = string.Empty;
    public string CorpoHtmlSnapshot { get; private set; } = string.Empty;
    public OrigemTemplateEmail OrigemTemplate { get; private set; }
    public string? ResponderParaSnapshot { get; private set; }
    public int QuantidadeTentativas { get; private set; }
    public DateTime? ProximaTentativaEmUtc { get; private set; }
    public DateTime? ProcessamentoIniciadoEmUtc { get; private set; }
    public DateTime? EnviadaEmUtc { get; private set; }
    public string? ProvedorMensagemId { get; private set; }
    public string? UltimoErroSeguro { get; private set; }
    public TipoTentativaNotificacaoEmail TipoProximaTentativa { get; private set; }
    public Guid? ProximaTentativaSolicitadaPorUsuarioId { get; private set; }
    public long Versao { get; private set; } = 1;
    public IReadOnlyCollection<TentativaNotificacaoEmail> Tentativas => _tentativas;

    public void MarcarProcessando(DateTime agoraUtc)
    {
        if (Status != StatusNotificacaoEmail.Pendente || ProximaTentativaEmUtc > agoraUtc)
            throw new InvalidOperationException("A notificação não está disponível para processamento.");
        Status = StatusNotificacaoEmail.Processando;
        ProcessamentoIniciadoEmUtc = agoraUtc;
        ProximaTentativaEmUtc = null;
        Versao++;
        MarcarComoAtualizada();
    }

    public void RecuperarProcessamentoInterrompido(DateTime agoraUtc)
    {
        if (Status != StatusNotificacaoEmail.Processando) return;
        Status = StatusNotificacaoEmail.Pendente;
        ProcessamentoIniciadoEmUtc = null;
        ProximaTentativaEmUtc = agoraUtc;
        Versao++;
        MarcarComoAtualizada();
    }

    public TentativaNotificacaoEmail RegistrarSucesso(string provedorMensagemId, DateTime agoraUtc,
        TipoTentativaNotificacaoEmail tipo, Guid? solicitadoPorUsuarioId)
    {
        ExigirProcessando();
        QuantidadeTentativas++;
        Status = StatusNotificacaoEmail.Enviada;
        EnviadaEmUtc = agoraUtc;
        ProvedorMensagemId = provedorMensagemId;
        UltimoErroSeguro = null;
        Versao++;
        var tentativa = new TentativaNotificacaoEmail(EmpresaId, Id, QuantidadeTentativas, tipo,
            solicitadoPorUsuarioId, agoraUtc, ResultadoTentativaNotificacaoEmail.Enviada, provedorMensagemId, null);
        _tentativas.Add(tentativa);
        ProximaTentativaSolicitadaPorUsuarioId = null;
        MarcarComoAtualizada();
        return tentativa;
    }

    public TentativaNotificacaoEmail RegistrarFalha(string erroSeguro, bool temporaria, int maximoTentativas,
        DateTime agoraUtc, DateTime? proximaTentativaUtc, TipoTentativaNotificacaoEmail tipo,
        Guid? solicitadoPorUsuarioId)
    {
        ExigirProcessando();
        QuantidadeTentativas++;
        var podeTentar = temporaria && QuantidadeTentativas < maximoTentativas;
        Status = podeTentar ? StatusNotificacaoEmail.Pendente : StatusNotificacaoEmail.Falhou;
        ProximaTentativaEmUtc = podeTentar ? proximaTentativaUtc : null;
        UltimoErroSeguro = erroSeguro.Length <= 500 ? erroSeguro : erroSeguro[..500];
        Versao++;
        var resultado = podeTentar ? ResultadoTentativaNotificacaoEmail.FalhaTemporaria : ResultadoTentativaNotificacaoEmail.FalhaTerminal;
        var tentativa = new TentativaNotificacaoEmail(EmpresaId, Id, QuantidadeTentativas, tipo,
            solicitadoPorUsuarioId, agoraUtc, resultado, null, UltimoErroSeguro);
        _tentativas.Add(tentativa);
        if (podeTentar)
        {
            TipoProximaTentativa = TipoTentativaNotificacaoEmail.Automatica;
            ProximaTentativaSolicitadaPorUsuarioId = null;
        }
        MarcarComoAtualizada();
        return tentativa;
    }

    public void PrepararReenvioManual(string? destinatarioAtual, Guid usuarioId, DateTime agoraUtc)
    {
        if (Status == StatusNotificacaoEmail.Enviada) throw new InvalidOperationException("Uma notificação aceita pelo provedor não pode ser reenviada.");
        if (Status is not (StatusNotificacaoEmail.Falhou or StatusNotificacaoEmail.SemDestinatario))
            throw new InvalidOperationException("Somente notificações com falha ou sem destinatário podem ser reenviadas.");
        if (Status == StatusNotificacaoEmail.SemDestinatario)
            DestinatarioEmailSnapshot = NormalizarEmail(destinatarioAtual) ?? throw new InvalidOperationException("O cliente continua sem e-mail cadastrado.");
        Status = StatusNotificacaoEmail.Pendente;
        ProximaTentativaEmUtc = agoraUtc;
        UltimoErroSeguro = null;
        TipoProximaTentativa = TipoTentativaNotificacaoEmail.Manual;
        ProximaTentativaSolicitadaPorUsuarioId = usuarioId;
        Versao++;
        MarcarComoAtualizada();
    }

    private void ExigirProcessando()
    {
        if (Status != StatusNotificacaoEmail.Processando) throw new InvalidOperationException("A notificação não está em processamento.");
    }

    private static string? NormalizarEmail(string? email) => string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
}

public sealed class TentativaNotificacaoEmail : EntidadeEmpresaBase
{
    private TentativaNotificacaoEmail() { }
    internal TentativaNotificacaoEmail(Guid empresaId, Guid notificacaoEmailId, int numero,
        TipoTentativaNotificacaoEmail tipo, Guid? solicitadoPorUsuarioId, DateTime concluidaEmUtc,
        ResultadoTentativaNotificacaoEmail resultado, string? provedorMensagemId, string? erroSeguro)
        : base(Guid.NewGuid(), empresaId)
    {
        NotificacaoEmailId = notificacaoEmailId;
        Numero = numero;
        Tipo = tipo;
        SolicitadoPorUsuarioId = solicitadoPorUsuarioId;
        IniciadaEmUtc = concluidaEmUtc;
        ConcluidaEmUtc = concluidaEmUtc;
        Resultado = resultado;
        ProvedorMensagemId = provedorMensagemId;
        ErroSeguro = erroSeguro;
    }
    public Guid NotificacaoEmailId { get; private set; }
    public int Numero { get; private set; }
    public TipoTentativaNotificacaoEmail Tipo { get; private set; }
    public Guid? SolicitadoPorUsuarioId { get; private set; }
    public DateTime IniciadaEmUtc { get; private set; }
    public DateTime ConcluidaEmUtc { get; private set; }
    public ResultadoTentativaNotificacaoEmail Resultado { get; private set; }
    public string? ProvedorMensagemId { get; private set; }
    public string? ErroSeguro { get; private set; }
}
