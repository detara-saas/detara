namespace Detara.Domain.Entidades;

public abstract class EntidadeEmpresaBase : EntidadeBase
{
    protected EntidadeEmpresaBase()
    {
    }

    protected EntidadeEmpresaBase(Guid id, Guid empresaId)
        : base(id)
    {
        if (empresaId == Guid.Empty)
        {
            throw new ArgumentException("A empresa deve ser informada.", nameof(empresaId));
        }

        EmpresaId = empresaId;
    }

    public Guid EmpresaId { get; protected set; }
}
