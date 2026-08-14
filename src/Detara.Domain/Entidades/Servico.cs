namespace Detara.Domain.Entidades;

public sealed class Servico : EntidadeEmpresaBase
{
    private readonly List<PacoteServico> _pacotes = [];

    private Servico() { }

    public Servico(
        Guid empresaId,
        Guid categoriaServicoId,
        string nome,
        string? descricao,
        decimal? precoBase,
        int? duracaoEstimadaMinutos,
        int ordem)
        : base(Guid.NewGuid(), empresaId) =>
        Atualizar(categoriaServicoId, nome, descricao, precoBase, duracaoEstimadaMinutos, ordem);

    public Guid CategoriaServicoId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public decimal? PrecoBase { get; private set; }
    public int? DuracaoEstimadaMinutos { get; private set; }
    public int Ordem { get; private set; }
    public CategoriaServico CategoriaServico { get; private set; } = null!;
    public IReadOnlyCollection<PacoteServico> Pacotes => _pacotes;

    public void Atualizar(
        Guid categoriaServicoId,
        string nome,
        string? descricao,
        decimal? precoBase,
        int? duracaoEstimadaMinutos,
        int ordem)
    {
        CategoriaServicoId = categoriaServicoId != Guid.Empty
            ? categoriaServicoId
            : throw new ArgumentException("A categoria deve ser informada.", nameof(categoriaServicoId));
        Nome = TextoCatalogo.Exigir(nome, 160, nameof(nome), 2);
        Descricao = TextoCatalogo.NormalizarOpcional(descricao, 2000);
        PrecoBase = precoBase is null or >= 0
            ? precoBase
            : throw new ArgumentException("O preço base não pode ser negativo.", nameof(precoBase));
        DuracaoEstimadaMinutos = duracaoEstimadaMinutos is null or > 0 and <= 43200
            ? duracaoEstimadaMinutos
            : throw new ArgumentException("A duração deve estar entre 1 e 43.200 minutos.", nameof(duracaoEstimadaMinutos));
        Ordem = ordem >= 0 ? ordem : throw new ArgumentException("A ordem não pode ser negativa.", nameof(ordem));
        MarcarComoAtualizada();
    }
}
