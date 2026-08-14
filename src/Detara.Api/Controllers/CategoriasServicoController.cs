using Detara.Application.Catalogo;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Catalogo;
using Detara.Contracts.Clientes;
using Detara.Contracts.Comum;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/categorias-servico")]
public sealed class CategoriasServicoController(ISender sender) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissoes.ServicosVisualizar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<CategoriaServicoResponse>>>> Listar([FromQuery] bool? ehAtivo = null, CancellationToken ct = default)
    {
        var itens = await sender.Send(new ListarCategoriasServicoQuery(ehAtivo), ct);
        return Ok(RespostaApi<IReadOnlyCollection<CategoriaServicoResponse>>.Ok(itens.Select(Mapear).ToArray()));
    }

    [HttpPost, Authorize(Policy = Permissoes.ServicosCriar)]
    public async Task<ActionResult<RespostaApi<CategoriaServicoResponse>>> Criar(SalvarCategoriaServicoRequest request, CancellationToken ct)
    {
        var item = await sender.Send(new CriarCategoriaServicoCommand(request.Nome, request.Descricao, request.Ordem), ct);
        return StatusCode(StatusCodes.Status201Created, RespostaApi<CategoriaServicoResponse>.Ok(Mapear(item), "Categoria cadastrada com sucesso."));
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Permissoes.ServicosEditar)]
    public async Task<ActionResult<RespostaApi<CategoriaServicoResponse>>> Atualizar(Guid id, SalvarCategoriaServicoRequest request, CancellationToken ct)
    {
        var item = await sender.Send(new AtualizarCategoriaServicoCommand(id, request.Nome, request.Descricao, request.Ordem), ct);
        return Ok(RespostaApi<CategoriaServicoResponse>.Ok(Mapear(item), "Categoria atualizada com sucesso."));
    }

    [HttpPatch("{id:guid}/status"), Authorize(Policy = Permissoes.ServicosEditar)]
    public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusRequest request, CancellationToken ct)
    {
        await sender.Send(new AlterarStatusCategoriaServicoCommand(id, request.EhAtivo), ct); return NoContent();
    }

    private static CategoriaServicoResponse Mapear(CategoriaServicoResultado x) => new(x.Id, x.Nome, x.Descricao, x.Ordem, x.QuantidadeServicos, x.EhAtivo);
}
