namespace Detara.Domain.Entidades;

public abstract class EntidadeBase
{
    protected EntidadeBase()
    {
    }

    protected EntidadeBase(Guid id)
    {
        Id = id;
        CriadoEmUtc = DateTime.UtcNow;
        EhAtivo = true;
    }

    public Guid Id { get; protected set; }
    public DateTime CriadoEmUtc { get; protected set; }
    public DateTime? AtualizadoEmUtc { get; protected set; }
    public bool EhAtivo { get; protected set; }

    public void Desativar()
    {
        EhAtivo = false;
        MarcarComoAtualizada();
    }

    public void Ativar()
    {
        EhAtivo = true;
        MarcarComoAtualizada();
    }

    protected void MarcarComoAtualizada() => AtualizadoEmUtc = DateTime.UtcNow;
}
