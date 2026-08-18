using Detara.Application.Atendimento;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Catalogo;
using Detara.Contracts.Comum;
using Detara.Domain.Atendimento;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/orcamentos")]
public sealed class OrcamentosController(ISender sender) : ControllerBase
{
    [HttpGet, Authorize(Policy = Permissoes.OrcamentosVisualizar)]
    public async Task<ActionResult<RespostaApi<PaginaResponse<OrcamentoListaResponse>>>> Listar([FromQuery] int pagina = 1,
        [FromQuery] int tamanhoPagina = 25, [FromQuery] StatusOrcamentoContrato? status = null,
        [FromQuery] string? pesquisa = null, CancellationToken ct = default)
    {
        var resultado = await sender.Send(new ListarOrcamentosQuery(pagina, tamanhoPagina,
            status.HasValue ? (StatusEfetivoOrcamento)(int)status.Value : null, pesquisa), ct);
        var resposta = new PaginaResponse<OrcamentoListaResponse>(resultado.Itens.Select(MapearLista).ToArray(),
            resultado.Pagina, resultado.TamanhoPagina, resultado.TotalItens, resultado.TotalPaginas);
        return Ok(RespostaApi<PaginaResponse<OrcamentoListaResponse>>.Ok(resposta));
    }

    [HttpGet("contexto"), Authorize(Policy = Permissoes.OrcamentosCriar)]
    public async Task<ActionResult<RespostaApi<ContextoOrcamentoResponse>>> Contexto(CancellationToken ct) { var x = await sender.Send(new ObterContextoOrcamentoQuery(), ct); return Ok(RespostaApi<ContextoOrcamentoResponse>.Ok(new(x.HojeLocal, x.ValidadeSugerida))); }

    [HttpGet("{id:guid}"), Authorize(Policy = Permissoes.OrcamentosVisualizar)]
    public async Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> Obter(Guid id, CancellationToken ct) =>
        Ok(RespostaApi<OrcamentoDetalheResponse>.Ok(MapearDetalhe(await sender.Send(new ObterOrcamentoQuery(id), ct))));

    [HttpPost, Authorize(Policy = Permissoes.OrcamentosCriar)]
    public async Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> Criar(SalvarOrcamentoRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new CriarOrcamentoCommand(request.ClienteId, request.VeiculoId, request.AgendamentoOrigemId,
            request.ValidoAte, request.ObservacaoCliente, request.ObservacaoInterna, request.Condicoes, request.Desconto,
            request.Acrescimo, request.Itens.Select(Mapear).ToArray()), ct);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Orcamento.Id }, RespostaApi<OrcamentoDetalheResponse>.Ok(MapearDetalhe(resultado), "Rascunho criado com sucesso."));
    }

    [HttpPut("{id:guid}"), Authorize(Policy = Permissoes.OrcamentosEditar)]
    public async Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> Atualizar(Guid id, SalvarOrcamentoRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new AtualizarOrcamentoCommand(id, request.ClienteId, request.VeiculoId, request.AgendamentoOrigemId,
            request.ValidoAte, request.ObservacaoCliente, request.ObservacaoInterna, request.Condicoes, request.Desconto,
            request.Acrescimo, request.Itens.Select(Mapear).ToArray()), ct);
        return Ok(RespostaApi<OrcamentoDetalheResponse>.Ok(MapearDetalhe(resultado), "Rascunho atualizado com sucesso."));
    }

    [HttpPost("{id:guid}/emitir"), Authorize(Policy = Permissoes.OrcamentosEditar)]
    public Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> Emitir(Guid id, RegistrarTransicaoOrcamentoRequest request, CancellationToken ct) => Transicao(sender.Send(new EmitirOrcamentoCommand(id, request.Observacao), ct), "Orçamento emitido com sucesso.");
    [HttpPost("{id:guid}/aprovar"), Authorize(Policy = Permissoes.OrcamentosEditar)]
    public Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> Aprovar(Guid id, RegistrarTransicaoOrcamentoRequest request, CancellationToken ct) => Transicao(sender.Send(new AprovarOrcamentoCommand(id, request.Observacao), ct), "Aprovação registrada com sucesso.");
    [HttpPost("{id:guid}/recusar"), Authorize(Policy = Permissoes.OrcamentosEditar)]
    public Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> Recusar(Guid id, RegistrarTransicaoOrcamentoRequest request, CancellationToken ct) => Transicao(sender.Send(new RecusarOrcamentoCommand(id, request.Observacao), ct), "Recusa registrada com sucesso.");
    [HttpPost("{id:guid}/cancelar"), Authorize(Policy = Permissoes.OrcamentosEditar)]
    public Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> Cancelar(Guid id, RegistrarTransicaoOrcamentoRequest request, CancellationToken ct) => Transicao(sender.Send(new CancelarOrcamentoCommand(id, request.Observacao), ct), "Orçamento cancelado com sucesso.");

    [HttpPost("{id:guid}/nova-proposta"), Authorize(Policy = Permissoes.OrcamentosCriar)]
    public async Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> NovaProposta(Guid id, CancellationToken ct)
    {
        var resultado = await sender.Send(new CriarNovaPropostaCommand(id), ct);
        return CreatedAtAction(nameof(Obter), new { id = resultado.Orcamento.Id }, RespostaApi<OrcamentoDetalheResponse>.Ok(MapearDetalhe(resultado), "Nova proposta criada como rascunho. O orçamento anterior não foi alterado."));
    }

    [HttpGet("{id:guid}/pdf"), Authorize(Policy = Permissoes.OrcamentosVisualizar)]
    public async Task<IActionResult> Pdf(Guid id, CancellationToken ct) { var pdf = await sender.Send(new GerarPdfOrcamentoQuery(id), ct); return File(pdf.Conteudo, "application/pdf", pdf.NomeArquivo); }

    [HttpGet("clientes"), Authorize(Policy = Permissoes.OrcamentosCriar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<ClienteOrcamentoResponse>>>> BuscarClientes([FromQuery] string pesquisa, CancellationToken ct) =>
        Ok(RespostaApi<IReadOnlyCollection<ClienteOrcamentoResponse>>.Ok((await sender.Send(new BuscarClientesOrcamentoQuery(pesquisa), ct)).Select(x => new ClienteOrcamentoResponse(x.Id, x.Nome, x.Documento, x.Telefone)).ToArray()));
    [HttpGet("clientes/{clienteId:guid}/veiculos"), Authorize(Policy = Permissoes.OrcamentosCriar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<VeiculoOrcamentoResponse>>>> Veiculos(Guid clienteId, CancellationToken ct) =>
        Ok(RespostaApi<IReadOnlyCollection<VeiculoOrcamentoResponse>>.Ok((await sender.Send(new ListarVeiculosOrcamentoQuery(clienteId), ct)).Select(x => new VeiculoOrcamentoResponse(x.Id, x.Descricao, x.Placa)).ToArray()));
    [HttpGet("catalogo"), Authorize(Policy = Permissoes.OrcamentosCriar)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<ItemCatalogoOrcamentoResponse>>>> Catalogo([FromQuery] string? pesquisa = null, CancellationToken ct = default) =>
        Ok(RespostaApi<IReadOnlyCollection<ItemCatalogoOrcamentoResponse>>.Ok((await sender.Send(new BuscarCatalogoOrcamentoQuery(pesquisa), ct)).Select(Mapear).ToArray()));
    [HttpGet("agendamentos/{agendamentoId:guid}/origem"), Authorize(Policy = Permissoes.OrcamentosCriar)]
    public async Task<ActionResult<RespostaApi<OrigemAgendamentoOrcamentoResponse>>> OrigemAgendamento(Guid agendamentoId, CancellationToken ct)
    {
        var x = await sender.Send(new ObterOrigemAgendamentoOrcamentoQuery(agendamentoId), ct);
        return Ok(RespostaApi<OrigemAgendamentoOrcamentoResponse>.Ok(new(x.Id, x.ClienteId, x.ClienteNome, x.VeiculoId,
            x.VeiculoDescricao, x.VeiculoPlaca, x.Itens.Select(i => new ItemCatalogoOrcamentoResponse((TipoItemOrcamentoContrato)(int)i.TipoItem,
                i.ItemCatalogoId, i.Nome, i.Descricao, (TipoPrecificacaoCatalogo)(int)i.TipoPrecificacao, i.PrecoReferencia)).ToArray())));
    }

    private async Task<ActionResult<RespostaApi<OrcamentoDetalheResponse>>> Transicao(Task<OrcamentoDetalheVisualizacao> tarefa, string mensagem) =>
        Ok(RespostaApi<OrcamentoDetalheResponse>.Ok(MapearDetalhe(await tarefa), mensagem));
    private static ItemOrcamentoEntrada Mapear(OrcamentoItemRequest x) => new((TipoItemOrcamento)(int)x.TipoItem, x.ItemCatalogoId, x.Nome, x.Descricao, x.ValorUnitario, x.Quantidade, x.Observacao);
    private static ItemCatalogoOrcamentoResponse Mapear(ItemCatalogoAtendimentoInterno x) => new((TipoItemOrcamentoContrato)(int)x.TipoItem, x.Id, x.Nome, x.Descricao, (TipoPrecificacaoCatalogo)(int)x.TipoPrecificacao, x.PrecoReferencia);
    private static OrcamentoListaResponse MapearLista(OrcamentoListaVisualizacao x) => new(x.Orcamento.Id, x.Orcamento.Codigo,
        x.Orcamento.ClienteNome, x.Orcamento.VeiculoDescricao, x.Orcamento.VeiculoPlaca, x.Orcamento.EmitidoEmUtc,
        x.Orcamento.ValidoAte, x.Orcamento.Total, (StatusOrcamentoContrato)(int)x.StatusEfetivo);

    internal static OrcamentoDetalheResponse MapearDetalhePublico(OrcamentoDetalheVisualizacao x) => MapearDetalhe(x);

    private static OrcamentoDetalheResponse MapearDetalhe(OrcamentoDetalheVisualizacao x)
    {
        var o = x.Orcamento;
        var itens = o.Itens.Select(i => new OrcamentoItemResponse(i.Id, (TipoItemOrcamentoContrato)(int)i.TipoItem,
            i.ItemCatalogoId, i.Nome, i.Descricao, i.TipoPrecificacaoReferencia.HasValue ? (TipoPrecificacaoCatalogo)(int)i.TipoPrecificacaoReferencia.Value : null,
            i.PrecoReferencia, i.ValorUnitario, i.Quantidade, i.ValorUnitario * i.Quantidade, i.Ordem, i.Observacao)).ToArray();
        return new(o.Id, o.Codigo, o.ClienteId, o.ClienteNome, o.ClienteDocumento, o.ClienteTelefone, o.VeiculoId,
            o.VeiculoDescricao, o.VeiculoPlaca, o.AgendamentoOrigemId, o.OrdemServicoOrigemId, o.OrdemServicoId,
            (StatusOrcamentoContrato)(int)x.StatusEfetivo,
            o.ValidoAte, o.ObservacaoCliente, o.ObservacaoInterna, o.Condicoes, itens.Sum(i => i.Subtotal), o.Desconto,
            o.Acrescimo, itens.Sum(i => i.Subtotal) - o.Desconto + o.Acrescimo, o.CriadoEmUtc, o.AtualizadoEmUtc,
            o.EmitidoEmUtc, o.AprovadoEmUtc, o.RecusadoEmUtc, o.CanceladoEmUtc, o.SubstituidoEmUtc, o.AprovadoPorUsuarioId,
            itens, x.Historico.Select(h => new HistoricoStatusOrcamentoResponse(h.Historico.Id,
                (StatusOrcamentoContrato)(int)h.Historico.Status, h.Historico.DataUtc, h.Historico.UsuarioId, h.UsuarioNome,
                h.Historico.Observacao)).ToArray(), Mapear(o.Origem), Mapear(o.Substituto));
    }
    private static ReferenciaOrcamentoResponse? Mapear(ReferenciaOrcamentoResultado? x) => x is null ? null : new(x.Id, x.Codigo, (StatusOrcamentoContrato)(int)x.Status);
}
