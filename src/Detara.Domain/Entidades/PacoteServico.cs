namespace Detara.Domain.Entidades;

public sealed class PacoteServico : EntidadeEmpresaBase
{
    private PacoteServico() { }

    internal PacoteServico(Guid empresaId, Guid pacoteId, Guid servicoId, int ordem)
        : base(Guid.NewGuid(), empresaId)
    {
        PacoteId = pacoteId;
        ServicoId = servicoId;
        Ordem = ordem;
    }

    public Guid PacoteId { get; private set; }
    public Guid ServicoId { get; private set; }
    public int Ordem { get; private set; }
    public Pacote Pacote { get; private set; } = null!;
    public Servico Servico { get; private set; } = null!;
}
