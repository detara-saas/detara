using Detara.Application.Plataforma;
using Detara.Contracts.Comum;
using Detara.Contracts.Plataforma;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Detara.Api.Controllers;

[ApiController]
[AllowAnonymous]
[EnableRateLimiting("convite-administrador")]
[Route("api/convites/administrador")]
public sealed class ConvitesAdministradoresEmpresaController(ISender sender) : ControllerBase
{
    [HttpPost("validar")]
    public async Task<ActionResult<RespostaApi<ConviteAdministradorValidadoResponse>>> Validar(
        ValidarConviteAdministradorRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new ValidarConviteAdministradorEmpresaQuery(request.Token),
            cancellationToken);
        return Ok(RespostaApi<ConviteAdministradorValidadoResponse>.Ok(new(
            resultado.EmpresaNome,
            resultado.EmailMascarado,
            resultado.ExpiraEmUtc)));
    }

    [HttpPost("aceitar")]
    public async Task<ActionResult<RespostaApi<object>>> Aceitar(
        AceitarConviteAdministradorRequest request,
        CancellationToken cancellationToken)
    {
        await sender.Send(new AceitarConviteAdministradorEmpresaCommand(
            request.Token,
            request.Senha,
            HttpContext.TraceIdentifier), cancellationToken);
        return Ok(RespostaApi<object>.Ok(new { }, "Conta ativada. Entre com sua empresa, e-mail e nova senha."));
    }
}
