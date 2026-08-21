namespace Detara.Domain.Entidades;

public sealed class Perfil : EntidadeEmpresaBase
{
    private readonly List<Permissao> _permissoes = [];

    private Perfil()
    {
    }

    public Perfil(Guid empresaId, string nome, string? descricao = null, bool ehSistema = false)
        : base(Guid.NewGuid(), empresaId)
    {
        AlterarNome(nome);
        Descricao = NormalizarOpcional(descricao);
        EhSistema = ehSistema;
    }

    public string Nome { get; private set; } = string.Empty;
    public string NomeNormalizado { get; private set; } = string.Empty;
    public string? Descricao { get; private set; }
    public bool EhSistema { get; private set; }
    public long Versao { get; private set; } = 1;
    public IReadOnlyCollection<Permissao> Permissoes => _permissoes;

    public void AlterarNome(string nome)
    {
        Nome = string.IsNullOrWhiteSpace(nome)
            ? throw new ArgumentException("O nome do perfil deve ser informado.", nameof(nome))
            : nome.Trim();
        NomeNormalizado = Nome.ToUpperInvariant();
        MarcarComoAtualizada();
    }

    public void Atualizar(
        string nome,
        string? descricao,
        IReadOnlyCollection<Permissao> permissoes,
        long versaoEsperada)
    {
        ExigirCustomizado();
        ExigirVersao(versaoEsperada);
        ArgumentNullException.ThrowIfNull(permissoes);
        AlterarNome(nome);
        Descricao = NormalizarOpcional(descricao);
        var ids = permissoes.Select(item => item.Id).ToHashSet();
        _permissoes.RemoveAll(item => !ids.Contains(item.Id));
        foreach (var permissao in permissoes)
        {
            ConcederPermissao(permissao);
        }

        Versao++;
        MarcarComoAtualizada();
    }

    public void AlterarStatus(bool ativar, long versaoEsperada)
    {
        ExigirCustomizado();
        ExigirVersao(versaoEsperada);
        if (ativar == EhAtivo)
        {
            return;
        }

        if (ativar) Ativar();
        else Desativar();
        Versao++;
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

    private void ExigirCustomizado()
    {
        if (EhSistema)
        {
            throw new InvalidOperationException("O perfil administrativo protegido não pode ser alterado.");
        }
    }

    private void ExigirVersao(long versaoEsperada)
    {
        if (versaoEsperada != Versao)
        {
            throw new InvalidOperationException("O perfil foi atualizado por outra operação.");
        }
    }

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
