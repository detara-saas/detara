using System.IdentityModel.Tokens.Jwt;
using Detara.Application.Abstracoes;

namespace Detara.Api.Autenticacao;

internal sealed class HttpUsuarioContexto(IHttpContextAccessor httpContextAccessor) : IUsuarioContexto
{
    private System.Security.Claims.ClaimsPrincipal? UsuarioAtual =>
        httpContextAccessor.HttpContext?.User;

    public bool EstaAutenticado =>
        UsuarioAtual?.Identity?.IsAuthenticated is true &&
        UsuarioId != Guid.Empty &&
        EmpresaId != Guid.Empty;

    public Guid UsuarioId => ObterGuid(JwtRegisteredClaimNames.Sub);

    public Guid EmpresaId => ObterGuid("empresa_id");

    private Guid ObterGuid(string claim)
    {
        var valor = UsuarioAtual?.FindFirst(claim)?.Value;
        return Guid.TryParse(valor, out var id) ? id : Guid.Empty;
    }
}
