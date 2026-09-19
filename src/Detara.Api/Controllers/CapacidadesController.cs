using Detara.Application.Capacidades;
using Detara.Contracts.Capacidades;
using Detara.Contracts.Comum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/configuracoes/capacidades")]
public sealed class CapacidadesController(IEmpresaCapacidadesServico capacidades) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespostaApi<SnapshotCapacidadesEmpresaResponse>>> Obter(
        CancellationToken cancellationToken)
    {
        var snapshot = await capacidades.ObterSnapshotAsync(cancellationToken);
        return Ok(RespostaApi<SnapshotCapacidadesEmpresaResponse>.Ok(new(
            snapshot.Segmento,
            snapshot.Capacidades.Select(item => new CapacidadeEmpresaResponse(
                item.Codigo,
                item.Nome,
                item.Categoria,
                item.Habilitada,
                item.Configuravel,
                item.Ordem)).ToArray())));
    }
}
