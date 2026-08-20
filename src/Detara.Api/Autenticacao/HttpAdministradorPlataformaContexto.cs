using System.IdentityModel.Tokens.Jwt;
using Detara.Application.Plataforma;

namespace Detara.Api.Autenticacao;

internal sealed class HttpAdministradorPlataformaContexto(IHttpContextAccessor httpContextAccessor)
    : IContextoAdministradorPlataforma
{
    private readonly System.Security.Claims.ClaimsPrincipal? _usuario =
        httpContextAccessor.HttpContext?.User;

    public Guid AdministradorPlataformaId => Guid.TryParse(
        _usuario?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
        out var id) ? id : Guid.Empty;

    public bool EstaAutenticado =>
        _usuario?.Identity?.IsAuthenticated is true &&
        _usuario.Identity.AuthenticationType == EsquemasAutenticacao.Plataforma &&
        AdministradorPlataformaId != Guid.Empty;
}
