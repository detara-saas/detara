using Detara.Domain.Entidades;

namespace Detara.Domain.Notificacoes;

public sealed class ComunicacaoCliente : EntidadeEmpresaBase
{
    private ComunicacaoCliente() { }

    public ComunicacaoCliente(Guid id, Guid empresaId, Guid clienteId, Guid ordemServicoId,
        CanalComunicacaoCliente canal, TipoComunicacaoCliente tipo, string mensagem,
        string? destinatarioSnapshot, OrigemComunicacaoCliente origem,
        Guid? solicitadoPorUsuarioId) : base(id, empresaId)
    {
        if (id == Guid.Empty) throw new ArgumentException("A comunicação deve possuir um identificador.", nameof(id));
        if (clienteId == Guid.Empty) throw new ArgumentException("O cliente deve ser informado.", nameof(clienteId));
        if (ordemServicoId == Guid.Empty) throw new ArgumentException("A ordem de serviço deve ser informada.", nameof(ordemServicoId));
        if (!Enum.IsDefined(canal)) throw new ArgumentException("O canal é inválido.", nameof(canal));
        if (!Enum.IsDefined(tipo)) throw new ArgumentException("O tipo é inválido.", nameof(tipo));
        if (!Enum.IsDefined(origem)) throw new ArgumentException("A origem é inválida.", nameof(origem));
        if (origem == OrigemComunicacaoCliente.Manual &&
            (!solicitadoPorUsuarioId.HasValue || solicitadoPorUsuarioId.Value == Guid.Empty))
            throw new ArgumentException("O usuário deve ser informado para uma comunicação manual.", nameof(solicitadoPorUsuarioId));

        var mensagemNormalizada = mensagem?.Trim() ?? string.Empty;
        if (mensagemNormalizada.Length is < 1 or > 100 * 1024)
            throw new ArgumentException("A mensagem deve possuir entre 1 e 100 KB.", nameof(mensagem));

        ClienteId = clienteId;
        OrdemServicoId = ordemServicoId;
        Canal = canal;
        Tipo = tipo;
        Mensagem = mensagemNormalizada;
        DestinatarioSnapshot = NormalizarDestinatario(destinatarioSnapshot);
        Origem = origem;
        SolicitadoPorUsuarioId = solicitadoPorUsuarioId;
        Status = StatusComunicacaoCliente.Pendente;
        if (DestinatarioSnapshot is null)
        {
            Status = StatusComunicacaoCliente.Falhou;
            UltimoErroSeguro = canal == CanalComunicacaoCliente.Email
                ? "O cliente não possui um e-mail válido cadastrado."
                : "O cliente não possui um WhatsApp válido cadastrado.";
        }
    }

    public Guid ClienteId { get; private set; }
    public Guid OrdemServicoId { get; private set; }
    public CanalComunicacaoCliente Canal { get; private set; }
    public TipoComunicacaoCliente Tipo { get; private set; }
    public string Mensagem { get; private set; } = string.Empty;
    public string? DestinatarioSnapshot { get; private set; }
    public StatusComunicacaoCliente Status { get; private set; }
    public OrigemComunicacaoCliente Origem { get; private set; }
    public Guid? SolicitadoPorUsuarioId { get; private set; }
    public DateTime? DataEnvioUtc { get; private set; }
    public DateTime? ProcessamentoIniciadoEmUtc { get; private set; }
    public string? ProvedorMensagemId { get; private set; }
    public string? UltimoErroSeguro { get; private set; }
    public long Versao { get; private set; } = 1;

    public void MarcarProcessando(DateTime agoraUtc)
    {
        if (Status != StatusComunicacaoCliente.Pendente || ProcessamentoIniciadoEmUtc.HasValue)
            throw new InvalidOperationException("A comunicação não está disponível para processamento.");
        ProcessamentoIniciadoEmUtc = agoraUtc;
        Versao++;
        MarcarComoAtualizada();
    }

    public void RecuperarProcessamentoInterrompido(DateTime agoraUtc, TimeSpan expiraEm)
    {
        if (Status != StatusComunicacaoCliente.Pendente ||
            ProcessamentoIniciadoEmUtc > agoraUtc.Subtract(expiraEm)) return;
        ProcessamentoIniciadoEmUtc = null;
        Versao++;
        MarcarComoAtualizada();
    }

    public void RegistrarEnvio(string provedorMensagemId, DateTime agoraUtc)
    {
        ExigirEmProcessamento();
        Status = StatusComunicacaoCliente.Enviado;
        DataEnvioUtc = agoraUtc;
        ProcessamentoIniciadoEmUtc = null;
        ProvedorMensagemId = NormalizarOpcional(provedorMensagemId, 200);
        UltimoErroSeguro = null;
        Versao++;
        MarcarComoAtualizada();
    }

    public void RegistrarFalha(string erroSeguro)
    {
        ExigirEmProcessamento();
        Status = StatusComunicacaoCliente.Falhou;
        ProcessamentoIniciadoEmUtc = null;
        UltimoErroSeguro = NormalizarOpcional(erroSeguro, 500) ?? "Falha ao enviar a comunicação.";
        Versao++;
        MarcarComoAtualizada();
    }

    public void ManterPendenteAposFalhaTemporaria(string erroSeguro)
    {
        ExigirEmProcessamento();
        ProcessamentoIniciadoEmUtc = null;
        UltimoErroSeguro = NormalizarOpcional(erroSeguro, 500) ?? "Falha temporária ao enviar a comunicação.";
        Versao++;
        MarcarComoAtualizada();
    }

    public void PrepararNovaTentativa(string destinatarioAtual)
    {
        if (Status != StatusComunicacaoCliente.Falhou)
            throw new InvalidOperationException("Somente uma comunicação com falha pode ser tentada novamente.");
        DestinatarioSnapshot = NormalizarDestinatario(destinatarioAtual)
            ?? throw new InvalidOperationException("O cliente continua sem destinatário válido.");
        Status = StatusComunicacaoCliente.Pendente;
        ProcessamentoIniciadoEmUtc = null;
        UltimoErroSeguro = null;
        Versao++;
        MarcarComoAtualizada();
    }

    private void ExigirEmProcessamento()
    {
        if (Status != StatusComunicacaoCliente.Pendente || !ProcessamentoIniciadoEmUtc.HasValue)
            throw new InvalidOperationException("A comunicação não está em processamento.");
    }

    private static string? NormalizarDestinatario(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var normalizado = valor.Trim();
        return normalizado.Length <= limite ? normalizado : normalizado[..limite];
    }
}
