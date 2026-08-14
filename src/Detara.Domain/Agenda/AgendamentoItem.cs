using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;

namespace Detara.Domain.Agenda;

public sealed class AgendamentoItem : EntidadeEmpresaBase
{
    private AgendamentoItem() { }

    internal AgendamentoItem(
        Guid empresaId,
        Guid agendamentoId,
        TipoItemAgendamento tipoItem,
        Guid itemCatalogoId,
        string nomeSnapshot,
        string? descricaoSnapshot,
        TipoPrecificacao tipoPrecificacaoSnapshot,
        decimal? precoReferenciaSnapshot,
        int? duracaoReferenciaMinutosSnapshot,
        int ordem)
        : base(Guid.NewGuid(), empresaId)
    {
        AgendamentoId = agendamentoId != Guid.Empty ? agendamentoId : throw new ArgumentException("O agendamento deve ser informado.", nameof(agendamentoId));
        TipoItem = Enum.IsDefined(tipoItem) ? tipoItem : throw new ArgumentException("O tipo do item é inválido.", nameof(tipoItem));
        ItemCatalogoId = itemCatalogoId != Guid.Empty ? itemCatalogoId : throw new ArgumentException("O item do catálogo deve ser informado.", nameof(itemCatalogoId));
        NomeSnapshot = NormalizarObrigatorio(nomeSnapshot, 160, nameof(nomeSnapshot));
        DescricaoSnapshot = NormalizarOpcional(descricaoSnapshot, 2000);
        TipoPrecificacaoSnapshot = tipoPrecificacaoSnapshot;
        PrecoReferenciaSnapshot = PrecificacaoCatalogo.Validar(tipoPrecificacaoSnapshot, precoReferenciaSnapshot, nameof(precoReferenciaSnapshot));
        DuracaoReferenciaMinutosSnapshot = duracaoReferenciaMinutosSnapshot is null or > 0 and <= 43200
            ? duracaoReferenciaMinutosSnapshot
            : throw new ArgumentException("A duração de referência é inválida.", nameof(duracaoReferenciaMinutosSnapshot));
        Ordem = ordem > 0 ? ordem : throw new ArgumentException("A ordem deve ser positiva.", nameof(ordem));
    }

    public Guid AgendamentoId { get; private set; }
    public TipoItemAgendamento TipoItem { get; private set; }
    public Guid ItemCatalogoId { get; private set; }
    public string NomeSnapshot { get; private set; } = string.Empty;
    public string? DescricaoSnapshot { get; private set; }
    public TipoPrecificacao TipoPrecificacaoSnapshot { get; private set; }
    public decimal? PrecoReferenciaSnapshot { get; private set; }
    public int? DuracaoReferenciaMinutosSnapshot { get; private set; }
    public int Ordem { get; private set; }
    public Agendamento Agendamento { get; private set; } = null!;

    private static string NormalizarObrigatorio(string valor, int limite, string parametro)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException("O nome do item deve ser informado.", parametro) : valor.Trim();
        return normalizado.Length <= limite ? normalizado : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.", parametro);
    }

    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var normalizado = valor.Trim();
        return normalizado.Length <= limite ? normalizado : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
}
