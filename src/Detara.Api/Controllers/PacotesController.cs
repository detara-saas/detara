using Detara.Application.Catalogo;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Catalogo;
using Detara.Contracts.Comum;
using Detara.Domain.Catalogo;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/pacotes")]
public sealed class PacotesController(ISender sender) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissoes.PacotesVisualizar)]
    public async Task<ActionResult<RespostaApi<PaginaResponse<PacoteListaResponse>>>> Listar([FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 25, [FromQuery] string? pesquisa = null, [FromQuery] bool? ehAtivo = null, CancellationToken ct = default)
    {
        var r = await sender.Send(new ListarPacotesQuery(new(pagina, tamanhoPagina, pesquisa, ehAtivo)), ct);
        return Ok(RespostaApi<PaginaResponse<PacoteListaResponse>>.Ok(new(r.Itens.Select(MapearLista).ToArray(), r.Pagina, r.TamanhoPagina, r.TotalItens, r.TotalPaginas)));
    }
    [HttpGet("{id:guid}"), Authorize(Policy = Permissoes.PacotesVisualizar)]
    public async Task<ActionResult<RespostaApi<PacoteDetalheResponse>>> Obter(Guid id, CancellationToken ct) => Ok(RespostaApi<PacoteDetalheResponse>.Ok(MapearDetalhe(await sender.Send(new ObterPacoteQuery(id), ct))));
    [HttpPost, Authorize(Policy = Permissoes.PacotesCriar)]
    public async Task<ActionResult<RespostaApi<PacoteDetalheResponse>>> Criar(SalvarPacoteRequest request, CancellationToken ct)
    {
        var item = await sender.Send(new CriarPacoteCommand(request.Nome, request.Descricao, MapearTipo(request.TipoPrecificacao), request.Preco, request.ServicoIds), ct);
        return CreatedAtAction(nameof(Obter), new { id = item.Id }, RespostaApi<PacoteDetalheResponse>.Ok(MapearDetalhe(item), "Pacote cadastrado com sucesso."));
    }
    [HttpPut("{id:guid}"), Authorize(Policy = Permissoes.PacotesEditar)]
    public async Task<ActionResult<RespostaApi<PacoteDetalheResponse>>> Atualizar(Guid id, SalvarPacoteRequest request, CancellationToken ct)
    {
        var item = await sender.Send(new AtualizarPacoteCommand(id, request.Nome, request.Descricao, MapearTipo(request.TipoPrecificacao), request.Preco, request.ServicoIds), ct);
        return Ok(RespostaApi<PacoteDetalheResponse>.Ok(MapearDetalhe(item), "Pacote atualizado com sucesso."));
    }
    [HttpPatch("{id:guid}/status"), Authorize(Policy = Permissoes.PacotesEditar)]
    public async Task<IActionResult> AlterarStatus(Guid id, AlterarStatusRequest request, CancellationToken ct) { await sender.Send(new AlterarStatusPacoteCommand(id, request.EhAtivo), ct); return NoContent(); }

    private static PacoteListaResponse MapearLista(PacoteListaItemResultado x) => new(x.Id, x.Nome, x.QuantidadeServicos, MapearTipo(x.TipoPrecificacao), x.Preco, x.SomaServicos, x.Economia, x.DuracaoEstimadaMinutos, x.EhAtivo);
    private static PacoteDetalheResponse MapearDetalhe(PacoteDetalheResultado x) => new(x.Id, x.Nome, x.Descricao, MapearTipo(x.TipoPrecificacao), x.Preco, x.SomaServicos, x.Economia, x.DuracaoEstimadaMinutos, x.CriadoEmUtc, x.AtualizadoEmUtc, x.EhAtivo, x.Servicos.Select(s => new PacoteServicoResponse(s.ServicoId, s.Nome, s.CategoriaNome, MapearTipo(s.TipoPrecificacao), s.PrecoBase, s.DuracaoEstimadaMinutos, s.Ordem, s.EhAtivo)).ToArray());
    private static TipoPrecificacao MapearTipo(TipoPrecificacaoCatalogo tipo) => (TipoPrecificacao)(int)tipo;
    private static TipoPrecificacaoCatalogo MapearTipo(TipoPrecificacao tipo) => (TipoPrecificacaoCatalogo)(int)tipo;
}
