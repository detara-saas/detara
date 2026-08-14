using Detara.Application.Catalogo;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Catalogo;
using Detara.Contracts.Comum;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/servicos")]
public sealed class ServicosController(ISender sender) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissoes.ServicosVisualizar)]
    public async Task<ActionResult<RespostaApi<PaginaResponse<ServicoListaResponse>>>> Listar([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 25, [FromQuery] string? pesquisa = null, [FromQuery] bool? ehAtivo = null, [FromQuery] Guid? categoriaServicoId = null, CancellationToken ct = default)
    {
        var r = await sender.Send(new ListarServicosQuery(new(pagina, tamanhoPagina, pesquisa, ehAtivo, categoriaServicoId)), ct);
        return Ok(RespostaApi<PaginaResponse<ServicoListaResponse>>.Ok(new(r.Itens.Select(MapearLista).ToArray(), r.Pagina, r.TamanhoPagina, r.TotalItens, r.TotalPaginas)));
    }

    [HttpGet("selecao"), Authorize(Policy = Permissoes.ServicosVisualizar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<ServicoSelecaoResponse>>>> Selecao([FromQuery] bool incluirInativos = false, CancellationToken ct = default)
    {
        var itens = await sender.Send(new ListarServicosSelecaoQuery(incluirInativos), ct);
        return Ok(RespostaApi<IReadOnlyCollection<ServicoSelecaoResponse>>.Ok(itens.Select(x => new ServicoSelecaoResponse(x.Id, x.Nome, x.CategoriaNome, x.PrecoBase, x.DuracaoEstimadaMinutos, x.EhAtivo)).ToArray()));
    }

    [HttpGet("{id:guid}"), Authorize(Policy = Permissoes.ServicosVisualizar)]
    public async Task<ActionResult<RespostaApi<ServicoDetalheResponse>>> Obter(Guid id, CancellationToken ct) => Ok(RespostaApi<ServicoDetalheResponse>.Ok(MapearDetalhe(await sender.Send(new ObterServicoQuery(id), ct))));

    [HttpPost, Authorize(Policy = Permissoes.ServicosCriar)]
    public async Task<ActionResult<RespostaApi<ServicoDetalheResponse>>> Criar(SalvarServicoRequest request, CancellationToken ct)
    {
        var item = await sender.Send(new CriarServicoCommand(request.CategoriaServicoId, request.Nome, request.Descricao, request.PrecoBase, request.DuracaoEstimadaMinutos, request.Ordem), ct);
        return CreatedAtAction(nameof(Obter), new { id = item.Id }, RespostaApi<ServicoDetalheResponse>.Ok(MapearDetalhe(item), "Serviço cadastrado com sucesso."));
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Permissoes.ServicosEditar)]
    public async Task<ActionResult<RespostaApi<ServicoDetalheResponse>>> Atualizar(Guid id, SalvarServicoRequest request, CancellationToken ct)
    {
        var item = await sender.Send(new AtualizarServicoCommand(id, request.CategoriaServicoId, request.Nome, request.Descricao, request.PrecoBase, request.DuracaoEstimadaMinutos, request.Ordem), ct);
        return Ok(RespostaApi<ServicoDetalheResponse>.Ok(MapearDetalhe(item), "Serviço atualizado com sucesso."));
    }

    [HttpPatch("{id:guid}/status"), Authorize(Policy = Permissoes.ServicosEditar)]
    public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusRequest request, CancellationToken ct) { await sender.Send(new AlterarStatusServicoCommand(id, request.EhAtivo), ct); return NoContent(); }

    private static ServicoListaResponse MapearLista(ServicoListaItemResultado x) => new(x.Id, x.Nome, x.CategoriaServicoId, x.CategoriaNome, x.PrecoBase, x.DuracaoEstimadaMinutos, x.EhAtivo);
    private static ServicoDetalheResponse MapearDetalhe(ServicoDetalheResultado x) => new(x.Id, x.CategoriaServicoId, x.CategoriaNome, x.Nome, x.Descricao, x.PrecoBase, x.DuracaoEstimadaMinutos, x.Ordem, x.CriadoEmUtc, x.AtualizadoEmUtc, x.EhAtivo);
}
