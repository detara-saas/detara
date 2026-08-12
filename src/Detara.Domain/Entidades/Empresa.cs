namespace Detara.Domain.Entidades;

public sealed class Empresa : EntidadeBase
{
    private Empresa()
    {
    }

    public Empresa(
        string nomeFantasia,
        string razaoSocial,
        string cpfCnpj,
        string slug,
        string? email = null,
        string? telefone = null)
        : base(Guid.NewGuid())
    {
        NomeFantasia = Exigir(nomeFantasia, nameof(nomeFantasia));
        RazaoSocial = Exigir(razaoSocial, nameof(razaoSocial));
        CpfCnpj = Exigir(cpfCnpj, nameof(cpfCnpj));
        Slug = Exigir(slug, nameof(slug)).ToLowerInvariant();
        Email = NormalizarOpcional(email);
        Telefone = NormalizarOpcional(telefone);
    }

    public string NomeFantasia { get; private set; } = string.Empty;
    public string RazaoSocial { get; private set; } = string.Empty;
    public string CpfCnpj { get; private set; } = string.Empty;
    public string? Email { get; private set; }
    public string? Telefone { get; private set; }
    public string Slug { get; private set; } = string.Empty;

    private static string Exigir(string valor, string parametro) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor deve ser informado.", parametro)
            : valor.Trim();

    private static string? NormalizarOpcional(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
