using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Detara.Api.Autenticacao;
using Microsoft.AspNetCore.Http;

namespace Detara.IntegrationTests.Security;

public sealed class HttpUsuarioContextoTests
{
    [Fact]
    public void UsuarioAutenticadoDepoisDaResolucaoDoServico_EhLidoDinamicamente()
    {
        var usuarioId = Guid.NewGuid();
        var empresaId = Guid.NewGuid();
        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var contexto = new HttpUsuarioContexto(accessor);

        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtRegisteredClaimNames.Sub, usuarioId.ToString()),
            new Claim("empresa_id", empresaId.ToString())
        ], "Bearer"));

        Assert.True(contexto.EstaAutenticado);
        Assert.Equal(usuarioId, contexto.UsuarioId);
        Assert.Equal(empresaId, contexto.EmpresaId);
    }
}
