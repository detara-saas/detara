using Detara.Application.Preferencias;
using Detara.Contracts.Comum;
using Detara.Contracts.Preferencias;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/preferencias")]
public sealed class PreferenciasController(ISender sender) : ControllerBase
{
    [HttpGet("me")]
    public async Task<ActionResult<RespostaApi<PreferenciasUsuarioResponse>>> Obter(
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(new ObterPreferenciasUsuarioQuery(), cancellationToken);
        return Ok(RespostaApi<PreferenciasUsuarioResponse>.Ok(Mapear(resultado)));
    }

    [HttpPut("me")]
    public async Task<ActionResult<RespostaApi<PreferenciasUsuarioResponse>>> Atualizar(
        AtualizarPreferenciasUsuarioRequest request,
        CancellationToken cancellationToken)
    {
        var resultado = await sender.Send(
            new AtualizarPreferenciasUsuarioCommand(
                request.Tema,
                request.Idioma,
                request.SidebarRecolhida,
                request.PaginaInicial,
                request.Favoritos),
            cancellationToken);
        return Ok(RespostaApi<PreferenciasUsuarioResponse>.Ok(
            Mapear(resultado),
            "Preferências atualizadas."));
    }

    private static PreferenciasUsuarioResponse Mapear(PreferenciasUsuarioResultado resultado) =>
        new(
            resultado.Tema,
            resultado.Idioma,
            resultado.SidebarRecolhida,
            resultado.PaginaInicial,
            resultado.Favoritos);
}
