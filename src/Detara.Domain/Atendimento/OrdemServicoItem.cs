using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed record ItemOrdemServicoSnapshot(
    TipoItemOrcamento TipoItem,
    Guid? ItemCatalogoId,
    Guid? OrcamentoOrigemId,
    Guid? OrcamentoItemOrigemId,
    string Nome,
    string? Descricao,
    decimal ValorUnitarioAutorizado,
    int Quantidade,
    int Ordem,
    OrigemComercialOrdemServico OrigemComercial,
    DateTime AutorizadoEmUtc,
    Guid AutorizadoPorUsuarioId,
    string? ObservacaoAutorizacao);

public sealed class OrdemServicoItem : EntidadeEmpresaBase
{
    private OrdemServicoItem() { }

    internal OrdemServicoItem(Guid empresaId, Guid ordemServicoId, ItemOrdemServicoSnapshot item)
        : base(Guid.NewGuid(), empresaId)
    {
        OrdemServicoId = ExigirId(ordemServicoId, nameof(ordemServicoId));
        TipoItem = Enum.IsDefined(item.TipoItem) ? item.TipoItem : throw new ArgumentException("O tipo do item é inválido.", nameof(item));
        if (TipoItem == TipoItemOrcamento.Personalizado && item.ItemCatalogoId.HasValue ||
            TipoItem != TipoItemOrcamento.Personalizado && !item.ItemCatalogoId.HasValue)
        {
            throw new ArgumentException("O vínculo com o catálogo não corresponde ao tipo do item.", nameof(item));
        }

        ItemCatalogoId = item.ItemCatalogoId;
        OrcamentoOrigemId = ValidarIdOpcional(item.OrcamentoOrigemId);
        OrcamentoItemOrigemId = ValidarIdOpcional(item.OrcamentoItemOrigemId);
        NomeSnapshot = NormalizarObrigatorio(item.Nome, 160);
        DescricaoSnapshot = NormalizarOpcional(item.Descricao, 2000);
        ValorUnitarioAutorizado = item.ValorUnitarioAutorizado >= 0
            ? decimal.Round(item.ValorUnitarioAutorizado, 2)
            : throw new ArgumentException("O valor autorizado não pode ser negativo.", nameof(item));
        Quantidade = item.Quantidade >= 1 ? item.Quantidade : throw new ArgumentException("A quantidade deve ser ao menos 1.", nameof(item));
        Ordem = item.Ordem >= 1 ? item.Ordem : throw new ArgumentException("A ordem deve ser positiva.", nameof(item));
        OrigemComercial = Enum.IsDefined(item.OrigemComercial) ? item.OrigemComercial : throw new ArgumentException("A origem comercial é inválida.", nameof(item));
        if (OrigemComercial == OrigemComercialOrdemServico.Orcamento && (!OrcamentoOrigemId.HasValue || !OrcamentoItemOrigemId.HasValue))
        {
            throw new ArgumentException("Itens autorizados por orçamento devem preservar a origem comercial.", nameof(item));
        }
        if (OrigemComercial == OrigemComercialOrdemServico.Cortesia && ValorUnitarioAutorizado != 0)
        {
            throw new ArgumentException("Uma cortesia deve possuir valor autorizado igual a zero.", nameof(item));
        }

        AutorizadoEmUtc = item.AutorizadoEmUtc.Kind != DateTimeKind.Local
            ? DateTime.SpecifyKind(item.AutorizadoEmUtc, DateTimeKind.Utc)
            : throw new ArgumentException("A data de autorização deve estar em UTC.", nameof(item));
        AutorizadoPorUsuarioId = ExigirId(item.AutorizadoPorUsuarioId, nameof(item));
        ObservacaoAutorizacao = NormalizarOpcional(item.ObservacaoAutorizacao, 1000);
    }

    public Guid OrdemServicoId { get; private set; }
    public TipoItemOrcamento TipoItem { get; private set; }
    public Guid? ItemCatalogoId { get; private set; }
    public Guid? OrcamentoOrigemId { get; private set; }
    public Guid? OrcamentoItemOrigemId { get; private set; }
    public string NomeSnapshot { get; private set; } = string.Empty;
    public string? DescricaoSnapshot { get; private set; }
    public decimal ValorUnitarioAutorizado { get; private set; }
    public int Quantidade { get; private set; }
    public int Ordem { get; private set; }
    public OrigemComercialOrdemServico OrigemComercial { get; private set; }
    public DateTime AutorizadoEmUtc { get; private set; }
    public Guid AutorizadoPorUsuarioId { get; private set; }
    public string? ObservacaoAutorizacao { get; private set; }
    public decimal Subtotal => ValorUnitarioAutorizado * Quantidade;
    public OrdemServico OrdemServico { get; private set; } = null!;

    private static Guid ExigirId(Guid id, string parametro) => id != Guid.Empty ? id : throw new ArgumentException("O identificador deve ser informado.", parametro);
    private static Guid? ValidarIdOpcional(Guid? id) => id is null || id != Guid.Empty ? id : throw new ArgumentException("O identificador opcional é inválido.");
    private static string NormalizarObrigatorio(string valor, int limite)
    {
        var texto = string.IsNullOrWhiteSpace(valor) ? throw new ArgumentException("O nome do item deve ser informado.") : valor.Trim();
        return texto.Length <= limite ? texto : throw new ArgumentException($"O nome deve possuir no máximo {limite} caracteres.");
    }
    private static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var texto = valor.Trim();
        return texto.Length <= limite ? texto : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
}
