namespace Detara.Domain.Entidades;

public sealed class CategoriaServico : EntidadeEmpresaBase
{
    private readonly List<Servico> _servicos = [];

    private CategoriaServico() { }

    public CategoriaServico(Guid empresaId, string nome, string? descricao, int ordem)
        : base(Guid.NewGuid(), empresaId) => Atualizar(nome, descricao, ordem);

    public string Nome { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public int Ordem { get; private set; }
    public IReadOnlyCollection<Servico> Servicos => _servicos;

    public void Atualizar(string nome, string? descricao, int ordem)
    {
        Nome = TextoCatalogo.Exigir(nome, 120, nameof(nome));
        Descricao = TextoCatalogo.NormalizarOpcional(descricao, 1000);
        Ordem = ordem >= 0 ? ordem : throw new ArgumentException("A ordem não pode ser negativa.", nameof(ordem));
        MarcarComoAtualizada();
    }
}
