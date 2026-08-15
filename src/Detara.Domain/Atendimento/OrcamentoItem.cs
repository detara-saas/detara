using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed record ItemOrcamentoSnapshot(
    TipoItemOrcamento TipoItem,
    Guid? ItemCatalogoId,
    string Nome,
    string? Descricao,
    TipoPrecificacao? TipoPrecificacaoReferencia,
    decimal? PrecoReferencia,
    decimal ValorUnitario,
    int Quantidade,
    int Ordem,
    string? Observacao);

public sealed class OrcamentoItem : EntidadeEmpresaBase
{
    private OrcamentoItem() { }

    internal OrcamentoItem(Guid empresaId, Guid orcamentoId, ItemOrcamentoSnapshot item)
        : base(Guid.NewGuid(), empresaId)
    {
        OrcamentoId = orcamentoId != Guid.Empty ? orcamentoId : throw new ArgumentException("O orçamento deve ser informado.", nameof(orcamentoId));
        TipoItem = Enum.IsDefined(item.TipoItem) ? item.TipoItem : throw new ArgumentException("O tipo do item é inválido.", nameof(item));
        if (TipoItem == TipoItemOrcamento.Personalizado && item.ItemCatalogoId.HasValue || TipoItem != TipoItemOrcamento.Personalizado && !item.ItemCatalogoId.HasValue)
            throw new ArgumentException("O vínculo com o catálogo não corresponde ao tipo do item.", nameof(item));
        ItemCatalogoId = item.ItemCatalogoId;
        NomeSnapshot = NormalizarObrigatorio(item.Nome, 160);
        DescricaoSnapshot = NormalizarOpcional(item.Descricao, 2000);
        TipoPrecificacaoReferenciaSnapshot = TipoItem == TipoItemOrcamento.Personalizado ? null : item.TipoPrecificacaoReferencia ?? throw new ArgumentException("A referência de precificação deve ser informada.", nameof(item));
        PrecoReferenciaSnapshot = item.PrecoReferencia is null or >= 0 ? item.PrecoReferencia : throw new ArgumentException("O preço de referência não pode ser negativo.", nameof(item));
        ValorUnitario = item.ValorUnitario >= 0 ? decimal.Round(item.ValorUnitario, 2) : throw new ArgumentException("O valor unitário não pode ser negativo.", nameof(item));
        Quantidade = item.Quantidade >= 1 ? item.Quantidade : throw new ArgumentException("A quantidade deve ser ao menos 1.", nameof(item));
        Ordem = item.Ordem >= 1 ? item.Ordem : throw new ArgumentException("A ordem deve ser positiva.", nameof(item));
        Observacao = NormalizarOpcional(item.Observacao, 1000);
    }

    public Guid OrcamentoId { get; private set; }
    public TipoItemOrcamento TipoItem { get; private set; }
    public Guid? ItemCatalogoId { get; private set; }
    public string NomeSnapshot { get; private set; } = string.Empty;
    public string? DescricaoSnapshot { get; private set; }
    public TipoPrecificacao? TipoPrecificacaoReferenciaSnapshot { get; private set; }
    public decimal? PrecoReferenciaSnapshot { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public int Quantidade { get; private set; }
    public int Ordem { get; private set; }
    public string? Observacao { get; private set; }
    public decimal Subtotal => Quantidade * ValorUnitario;
    public Orcamento Orcamento { get; private set; } = null!;

    internal ItemOrcamentoSnapshot CriarSnapshot() => new(TipoItem, ItemCatalogoId, NomeSnapshot, DescricaoSnapshot, TipoPrecificacaoReferenciaSnapshot, PrecoReferenciaSnapshot, ValorUnitario, Quantidade, Ordem, Observacao);

    private static string NormalizarObrigatorio(string valor, int limite)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException("O nome do item deve ser informado.") : valor.Trim();
        return normalizado.Length <= limite ? normalizado : throw new ArgumentException($"O nome deve possuir no máximo {limite} caracteres.");
    }

    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var normalizado = valor.Trim();
        return normalizado.Length <= limite ? normalizado : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
}
