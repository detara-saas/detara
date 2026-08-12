namespace Detara.Contracts.Autenticacao;

public sealed record LoginRequest(string SlugEmpresa, string Email, string Senha);

public sealed record LoginResponse(
    string Token,
    DateTime ExpiraEmUtc,
    Guid UsuarioId,
    Guid EmpresaId,
    string Nome,
    string Perfil,
    IReadOnlyCollection<string> Permissoes);
