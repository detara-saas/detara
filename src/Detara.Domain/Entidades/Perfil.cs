namespace Detara.Domain.Entidades;

public sealed class Perfil : EntidadeEmpresaBase
{
    private readonly List<Permissao> _permissoes = [];

    private Perfil()
    {
    }

    public Perfil(Guid empresaId, string nome)
        : base(Guid.NewGuid(), empresaId)
    {
        AlterarNome(nome);
    }

    public string Nome { get; private set; } = string.Empty;
    public IReadOnlyCollection<Permissao> Permissoes => _permissoes;

    public void AlterarNome(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome do perfil deve ser informado.", nameof(nome))
            : nome.Trim();
        MarcarComoAtualizada();
    }

    public void ConcederPermissao(Permissao permissao)
    {
        ArgumentNullException.ThrowIfNull(permissao);
        if (_permissoes.All(item => item.Id != permissao.Id))
        {
            _permissoes.Add(permissao);
            MarcarComoAtualizada();
        }
    }
}
