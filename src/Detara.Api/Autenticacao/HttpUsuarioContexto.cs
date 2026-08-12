using System.IdentityModel.Tokens.Jwt;
using Detara.Application.Abstracoes;

namespace Detara.Api.Autenticacao;

internal sealed class HttpUsuarioContexto(IHttpContextAccessor httpContextAccessor) : IUsuarioContexto
{
    private readonly System.Security.Claims.ClaimsPrincipal? _usuario =
        httpContextAccessor.HttpContext?.User;

    public bool EstaAutenticado => _usuario?.Identity?.IsAuthenticated is true;

    public Guid UsuarioId => ObterGuid(JwtRegisteredClaimNames.Sub);

    public Guid EmpresaId => ObterGuid("empresa_id");

    private Guid ObterGuid(string claim)
    {
        var valor = _usuario?.FindFirst(claim)?.Value;
        return Guid.TryParse(valor, out var id) ? id : Guid.Empty;
    }
}
