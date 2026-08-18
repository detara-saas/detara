using Detara.Domain.Entidades;

namespace Detara.Domain.Atendimento;

public sealed class ChecklistModeloItem : EntidadeEmpresaBase
{
    private ChecklistModeloItem()
    {
    }

    internal ChecklistModeloItem(
        Guid empresaId,
        Guid checklistModeloId,
        string descricao,
        int ordem)
        : base(Guid.NewGuid(), empresaId)
    {
        ChecklistModeloId = checklistModeloId != Guid.Empty
            ? checklistModeloId
            : throw new ArgumentException("O modelo deve ser informado.", nameof(checklistModeloId));
        Descricao = ChecklistModelo.NormalizarDescricaoItem(descricao);
        Ordem = ordem > 0
            ? ordem
            : throw new ArgumentOutOfRangeException(nameof(ordem), "A ordem deve ser maior que zero.");
    }

    public Guid ChecklistModeloId { get; private set; }
    public string Descricao { get; private set; } = string.Empty;
    public int Ordem { get; private set; }
    public ChecklistModelo ChecklistModelo { get; private set; } = null!;
}
