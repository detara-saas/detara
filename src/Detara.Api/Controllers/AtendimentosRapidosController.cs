using Detara.Api.Capacidades;
using Detara.Application.Atendimento;
using Detara.Application.FluxoOperacional;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Catalogo;
using Detara.Contracts.Comum;
using Detara.Domain.Atendimento;
using Detara.Domain.Financeiro;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/atendimentos-rapidos")]
[Authorize(Policy = PoliticasAtendimento.RegistrarAtendimentoRapido)]
public sealed class AtendimentosRapidosController(ISender sender) : ControllerBase
{
    [HttpGet("contexto")]
    public async Task<ActionResult<RespostaApi<ContextoAtendimentoRapidoResponse>>> Contexto(CancellationToken ct) =>
        Ok(RespostaApi<ContextoAtendimentoRapidoResponse>.Ok(new(
            await sender.Send(new ObterContextoAtendimentoRapidoQuery(), ct))));

    [HttpPost]
    public async Task<ActionResult<RespostaApi<AtendimentoRapidoResponse>>> Registrar(
        RegistrarAtendimentoRapidoRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new RegistrarAtendimentoRapidoCommand(request.SolicitacaoId,
            request.ClienteId, request.VeiculoId, (TipoItemOrcamento)(int)request.TipoItem,
            request.ItemCatalogoId, request.ValorPago, (FormaPagamento)(int)request.FormaPagamento,
            request.RealizadoEmLocal, request.Taxa, request.NumeroParcelas, request.ObservacaoPagamento), ct);
        return Ok(RespostaApi<AtendimentoRapidoResponse>.Ok(new(resultado.OrdemServicoId,
            resultado.Codigo, resultado.ContaReceberId), "Atendimento e pagamento registrados com sucesso."));
    }

    [HttpGet("clientes")]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<ClienteOrcamentoResponse>>>> Clientes(
        [FromQuery] string pesquisa, CancellationToken ct) =>
        Ok(RespostaApi<IReadOnlyCollection<ClienteOrcamentoResponse>>.Ok(
            (await sender.Send(new BuscarClientesOrcamentoQuery(pesquisa), ct))
            .Select(item => new ClienteOrcamentoResponse(item.Id, item.Nome, item.Documento, item.Telefone)).ToArray()));

    [HttpGet("clientes/{clienteId:guid}/veiculos")]
    [Authorize(Policy = PoliticasCapacidade.Veiculos)]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<VeiculoOrcamentoResponse>>>> Veiculos(
        Guid clienteId, CancellationToken ct) =>
        Ok(RespostaApi<IReadOnlyCollection<VeiculoOrcamentoResponse>>.Ok(
            (await sender.Send(new ListarVeiculosOrcamentoQuery(clienteId), ct))
            .Select(item => new VeiculoOrcamentoResponse(item.Id, item.Descricao, item.Placa)).ToArray()));

    [HttpGet("catalogo")]
    public async Task<ActionResult<RespostaApi<IReadOnlyCollection<ItemCatalogoOrcamentoResponse>>>> Catalogo(
        [FromQuery] string? pesquisa = null, CancellationToken ct = default) =>
        Ok(RespostaApi<IReadOnlyCollection<ItemCatalogoOrcamentoResponse>>.Ok(
            (await sender.Send(new BuscarCatalogoOrcamentoQuery(pesquisa), ct))
            .Select(item => new ItemCatalogoOrcamentoResponse((TipoItemOrcamentoContrato)(int)item.TipoItem,
                item.Id, item.Nome, item.Descricao, (TipoPrecificacaoCatalogo)(int)item.TipoPrecificacao,
                item.PrecoReferencia)).ToArray()));
}
