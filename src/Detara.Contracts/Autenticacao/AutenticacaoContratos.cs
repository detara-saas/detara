using System.Text.Json.Serialization;

namespace Detara.Contracts.Autenticacao;

public sealed record LoginRequest(string Email, string Senha);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "tipo")]
[JsonDerivedType(typeof(LoginAutenticadoResponse), "autenticado")]
[JsonDerivedType(typeof(SelecaoEmpresaNecessariaResponse), "selecionar_empresa")]
public abstract record LoginResponse;

public sealed record LoginAutenticadoResponse(
    string Token,
    DateTime ExpiraEmUtc,
    Guid UsuarioId,
    Guid EmpresaId,
    string Nome,
    string Perfil,
    IReadOnlyCollection<string> Permissoes) : LoginResponse;

public sealed record SelecaoEmpresaNecessariaResponse(
    string Challenge,
    DateTime ExpiraEmUtc,
    IReadOnlyCollection<EmpresaSelecaoResponse> Empresas) : LoginResponse;

public sealed record EmpresaSelecaoResponse(Guid EmpresaId, string NomeExibicao);

public sealed record SelecionarEmpresaRequest(string Challenge, Guid EmpresaId);
