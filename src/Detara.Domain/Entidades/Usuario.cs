namespace Detara.Domain.Entidades;

public sealed class Usuario : EntidadeEmpresaBase
{
    private Usuario()
    {
    }

    public Usuario(Guid empresaId, Guid perfilId, string nome, string email, string senhaHash)
        : base(Guid.NewGuid(), empresaId)
    {
        PerfilId = perfilId == Guid.Empty
            ? throw new ArgumentException("O perfil deve ser informado.", nameof(perfilId))
            : perfilId;
        Nome = Exigir(nome, nameof(nome));
        Email = Exigir(email, nameof(email)).ToLowerInvariant();
        SenhaHash = Exigir(senhaHash, nameof(senhaHash));
    }

    public Guid PerfilId { get; private set; }
    public string Nome { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;
    public string SenhaHash { get; private set; } = string.Empty;
    public Perfil Perfil { get; private set; } = null!;
    public Empresa Empresa { get; private set; } = null!;

    public void AlterarSenhaHash(string senhaHash)
    {
        SenhaHash = Exigir(senhaHash, nameof(senhaHash));
        MarcarComoAtualizada();
    }

    private static string Exigir(string valor, string parametro) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor deve ser informado.", parametro)
            : valor.Trim();
}
