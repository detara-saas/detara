using Detara.Application.Autenticacao;
using Detara.Contracts.Autenticacao;
using Detara.Contracts.Comum;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/autenticacao")]
public sealed class AutenticacaoController(ISender sender) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("login")]
    [HttpPost("login")]
    [ProducesResponseType(typeof(RespostaApi<LoginResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(RespostaApi<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(RespostaApi<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(RespostaApi<object>), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<RespostaApi<LoginResponse>>> Login(
        LoginRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new AutenticarCommand(request.SlugEmpresa, request.Email, request.Senha),
            cancellationToken);
        var response = new LoginResponse(
            resultado.Token,
            resultado.ExpiraEmUtc,
            resultado.UsuarioId,
            resultado.EmpresaId,
            resultado.Nome,
            resultado.Perfil,
            resultado.Permissoes);

        return Ok(RespostaApi<LoginResponse>.Ok(response, "Login realizado com sucesso."));
    }
}
