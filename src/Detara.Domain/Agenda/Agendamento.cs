using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;

namespace Detara.Domain.Agenda;

public sealed record ItemAgendamentoSnapshot(
    TipoItemAgendamento TipoItem,
    Guid ItemCatalogoId,
    string Nome,
    string? Descricao,
    TipoPrecificacao TipoPrecificacao,
    decimal? PrecoReferencia,
    int? DuracaoReferenciaMinutos);

public sealed class Agendamento : EntidadeEmpresaBase
{
    private readonly List<AgendamentoItem> _itens = [];
    private Agendamento() { }

    public Agendamento(
        Guid empresaId,
        Guid clienteId,
        string clienteNomeSnapshot,
        Guid veiculoId,
        string veiculoDescricaoSnapshot,
        string veiculoPlacaSnapshot,
        DateTime inicioUtc,
        int duracaoPlanejadaMinutos,
        string? observacaoSolicitante,
        string? observacaoInterna,
        IReadOnlyCollection<ItemAgendamentoSnapshot> itens)
        : this(empresaId, clienteId, clienteNomeSnapshot, veiculoId, veiculoDescricaoSnapshot,
            veiculoPlacaSnapshot, inicioUtc, duracaoPlanejadaMinutos, observacaoSolicitante,
            observacaoInterna, itens, false)
    {
    }

    private Agendamento(
        Guid empresaId,
        Guid clienteId,
        string clienteNomeSnapshot,
        Guid veiculoId,
        string veiculoDescricaoSnapshot,
        string veiculoPlacaSnapshot,
        DateTime inicioUtc,
        int duracaoPlanejadaMinutos,
        string? observacaoSolicitante,
        string? observacaoInterna,
        IReadOnlyCollection<ItemAgendamentoSnapshot> itens,
        bool permitirSemItens)
        : base(Guid.NewGuid(), empresaId)
    {
        ClienteId = ExigirId(clienteId, nameof(clienteId));
        ClienteNomeSnapshot = NormalizarObrigatorio(clienteNomeSnapshot, 160, nameof(clienteNomeSnapshot));
        VeiculoId = ExigirId(veiculoId, nameof(veiculoId));
        VeiculoDescricaoSnapshot = NormalizarObrigatorio(veiculoDescricaoSnapshot, 200, nameof(veiculoDescricaoSnapshot));
        VeiculoPlacaSnapshot = NormalizarObrigatorio(veiculoPlacaSnapshot, 10, nameof(veiculoPlacaSnapshot));
        Status = StatusAgendamento.Agendado;
        AtualizarPlanejamentoInterno(inicioUtc, duracaoPlanejadaMinutos, observacaoSolicitante,
            observacaoInterna, itens, permitirSemItens);
    }

    public static Agendamento CriarDeOrcamento(
        Guid empresaId,
        Guid clienteId,
        string clienteNomeSnapshot,
        Guid veiculoId,
        string veiculoDescricaoSnapshot,
        string veiculoPlacaSnapshot,
        DateTime inicioUtc,
        int duracaoPlanejadaMinutos,
        string? observacaoSolicitante,
        string? observacaoInterna,
        IReadOnlyCollection<ItemAgendamentoSnapshot> itens) =>
        new(empresaId, clienteId, clienteNomeSnapshot, veiculoId, veiculoDescricaoSnapshot,
            veiculoPlacaSnapshot, inicioUtc, duracaoPlanejadaMinutos, observacaoSolicitante,
            observacaoInterna, itens, permitirSemItens: itens.Count == 0);

    public Guid ClienteId { get; private set; }
    public string ClienteNomeSnapshot { get; private set; } = string.Empty;
    public Guid VeiculoId { get; private set; }
    public string VeiculoDescricaoSnapshot { get; private set; } = string.Empty;
    public string VeiculoPlacaSnapshot { get; private set; } = string.Empty;
    public DateTime InicioUtc { get; private set; }
    public int DuracaoPlanejadaMinutos { get; private set; }
    public StatusAgendamento Status { get; private set; }
    public string? ObservacaoSolicitante { get; private set; }
    public string? ObservacaoInterna { get; private set; }
    public string? MotivoCancelamento { get; private set; }
    public IReadOnlyCollection<AgendamentoItem> Itens => _itens;
    public DateTime FimUtc => InicioUtc.AddMinutes(DuracaoPlanejadaMinutos);

    public void AtualizarPlanejamento(
        DateTime inicioUtc,
        int duracaoPlanejadaMinutos,
        string? observacaoSolicitante,
        string? observacaoInterna,
        IReadOnlyCollection<ItemAgendamentoSnapshot> itens)
        => AtualizarPlanejamentoInterno(inicioUtc, duracaoPlanejadaMinutos, observacaoSolicitante,
            observacaoInterna, itens, false);

    private void AtualizarPlanejamentoInterno(
        DateTime inicioUtc,
        int duracaoPlanejadaMinutos,
        string? observacaoSolicitante,
        string? observacaoInterna,
        IReadOnlyCollection<ItemAgendamentoSnapshot> itens,
        bool permitirSemItens)
    {
        ExigirEditavel();
        InicioUtc = inicioUtc.Kind == DateTimeKind.Utc
            ? inicioUtc
            : throw new ArgumentException("O início deve estar em UTC.", nameof(inicioUtc));
        DuracaoPlanejadaMinutos = duracaoPlanejadaMinutos is > 0 and <= 43200
            ? duracaoPlanejadaMinutos
            : throw new ArgumentException("A duração planejada deve estar entre 1 e 43.200 minutos.", nameof(duracaoPlanejadaMinutos));
        ObservacaoSolicitante = NormalizarOpcional(observacaoSolicitante, 2000);
        ObservacaoInterna = NormalizarOpcional(observacaoInterna, 4000);
        SubstituirItens(itens, permitirSemItens);
        MarcarComoAtualizada();
    }

    public void Reagendar(DateTime inicioUtc, int duracaoPlanejadaMinutos)
    {
        ExigirEditavel();
        if (inicioUtc.Kind != DateTimeKind.Utc) throw new ArgumentException("O início deve estar em UTC.", nameof(inicioUtc));
        if (duracaoPlanejadaMinutos is <= 0 or > 43200) throw new ArgumentException("A duração planejada deve estar entre 1 e 43.200 minutos.", nameof(duracaoPlanejadaMinutos));
        InicioUtc = inicioUtc;
        DuracaoPlanejadaMinutos = duracaoPlanejadaMinutos;
        MarcarComoAtualizada();
    }

    public void AlterarStatus(StatusAgendamento novoStatus, string? motivoCancelamento = null)
    {
        if (!Enum.IsDefined(novoStatus) || !TransicaoPermitida(Status, novoStatus))
        {
            throw new InvalidOperationException($"A transição de {Status} para {novoStatus} não é permitida.");
        }

        Status = novoStatus;
        MotivoCancelamento = novoStatus == StatusAgendamento.Cancelado
            ? NormalizarOpcional(motivoCancelamento, 1000)
            : null;
        MarcarComoAtualizada();
    }

    public static bool TransicaoPermitida(StatusAgendamento atual, StatusAgendamento destino) => atual switch
    {
        StatusAgendamento.Agendado => destino is StatusAgendamento.Confirmado or StatusAgendamento.Compareceu or StatusAgendamento.Cancelado or StatusAgendamento.NaoCompareceu,
        StatusAgendamento.Confirmado => destino is StatusAgendamento.Compareceu or StatusAgendamento.Cancelado or StatusAgendamento.NaoCompareceu,
        StatusAgendamento.Compareceu => destino is StatusAgendamento.Concluido or StatusAgendamento.Cancelado,
        _ => false
    };

    private void ExigirEditavel()
    {
        if (Status is not (StatusAgendamento.Agendado or StatusAgendamento.Confirmado))
        {
            throw new InvalidOperationException("Somente agendamentos agendados ou confirmados podem ser editados ou reagendados.");
        }
    }

    private void SubstituirItens(IReadOnlyCollection<ItemAgendamentoSnapshot> itens, bool permitirSemItens)
    {
        if (itens.Count == 0 && !permitirSemItens)
            throw new ArgumentException("O agendamento deve possuir ao menos um serviço ou pacote.", nameof(itens));
        if (itens.Any(x => x.ItemCatalogoId == Guid.Empty) || itens.Select(x => (x.TipoItem, x.ItemCatalogoId)).Distinct().Count() != itens.Count)
        {
            throw new ArgumentException("Os itens devem ser válidos e não podem se repetir.", nameof(itens));
        }

        _itens.Clear();
        _itens.AddRange(itens.Select((item, indice) => new AgendamentoItem(
            EmpresaId, Id, item.TipoItem, item.ItemCatalogoId, item.Nome, item.Descricao,
            item.TipoPrecificacao, item.PrecoReferencia, item.DuracaoReferenciaMinutos, indice + 1)));
    }

    private static Guid ExigirId(Guid id, string parametro) => id != Guid.Empty ? id : throw new ArgumentException("O identificador deve ser informado.", parametro);
    private static string NormalizarObrigatorio(string valor, int limite, string parametro)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException("O valor deve ser informado.", parametro) : valor.Trim();
        return normalizado.Length <= limite ? normalizado : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.", parametro);
    }
    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var normalizado = valor.Trim();
        return normalizado.Length <= limite ? normalizado : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
}
