namespace Detara.Domain.Entidades;

public sealed class Permissao : EntidadeBase
{
    private Permissao()
    {
    }

    public Permissao(string codigo, string descricao)
        : base(Guid.NewGuid())
    {
        Codigo = string.IsNullOrWhiteSpace(codigo)
            ? throw new ArgumentException("O código da permissão deve ser informado.", nameof(codigo))
            : codigo.Trim();
        Descricao = string.IsNullOrWhiteSpace(descricao)
            ? throw new ArgumentException("A descrição deve ser informada.", nameof(descricao))
            : descricao.Trim();
    }

    public string Codigo { get; private set; } = string.Empty;
    public string Descricao { get; private set; } = string.Empty;
}
