using Detara.Application.Abstracoes;
using Detara.Application.Financeiro;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Financeiro;
using Detara.Domain.Financeiro;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[ApiController]
[Route("api/financeiro")]
public sealed class FinanceiroController(ISender sender) : ControllerBase
{
    [HttpGet("resumo"), Authorize(Policy = Permissoes.FinanceiroVisualizar)]
    public async Task<ActionResult<RespostaApi<ResumoFinanceiroResponse>>> Resumo(
        [FromQuery] DateOnly? inicio = null, [FromQuery] DateOnly? fim = null, CancellationToken ct = default)
    {
        var resultado = await sender.Send(new ObterResumoFinanceiroQuery(inicio, fim), ct);
        return Ok(RespostaApi<ResumoFinanceiroResponse>.Ok(new(resultado.Inicio, resultado.Fim,
            resultado.Faturado, resultado.RecebidoBruto, resultado.Taxas, resultado.ReceitaLiquidaRecebida,
            resultado.EmAbertoAtual, resultado.VencidoAtual, resultado.TicketMedio,
            resultado.FormasPagamento.Select(item => new FormaPagamentoResumoResponse(
                (FormaPagamentoContrato)(int)item.Forma, item.Valor, item.Quantidade)).ToArray())));
    }

    [HttpGet("contas-receber"), Authorize(Policy = Permissoes.FinanceiroVisualizar)]
    public async Task<ActionResult<RespostaApi<PaginaResponse<ContaReceberListaResponse>>>> Listar(
        [FromQuery] int pagina = 1, [FromQuery] int tamanhoPagina = 25,
        [FromQuery] StatusContaReceberContrato? status = null, [FromQuery] bool? vencida = null,
        [FromQuery] DateOnly? competenciaInicial = null, [FromQuery] DateOnly? competenciaFinal = null,
        [FromQuery] string? pesquisa = null, CancellationToken ct = default)
    {
        var resultado = await sender.Send(new ListarContasReceberQuery(pagina, tamanhoPagina,
            status.HasValue ? (StatusContaReceber)(int)status.Value : null, vencida,
            competenciaInicial, competenciaFinal, pesquisa), ct);
        var resposta = resultado.Itens.Select(item => new ContaReceberListaResponse(item.Id,
            item.OrdemServicoId, item.OrdemServicoCodigo, item.ClienteNome, item.VeiculoDescricao,
            item.VeiculoPlaca, item.DataCompetencia, item.DataVencimento, item.ValorOriginal,
            item.ValorRecebido, item.ValorOriginal - item.ValorRecebido,
            (StatusContaReceberContrato)(int)item.Status, item.Vencida)).ToArray();
        return Ok(RespostaApi<PaginaResponse<ContaReceberListaResponse>>.Ok(new(resposta,
            resultado.Pagina, resultado.TamanhoPagina, resultado.TotalItens, resultado.TotalPaginas)));
    }

    [HttpGet("contas-receber/{id:guid}"), Authorize(Policy = Permissoes.FinanceiroVisualizar)]
    public async Task<ActionResult<RespostaApi<ContaReceberDetalheResponse>>> Obter(Guid id, CancellationToken ct) =>
        Ok(RespostaApi<ContaReceberDetalheResponse>.Ok(Mapear(await sender.Send(new ObterContaReceberQuery(id), ct))));

    [HttpGet("contas-receber/por-ordem-servico/{ordemServicoId:guid}"), Authorize(Policy = Permissoes.FinanceiroVisualizar)]
    public async Task<ActionResult<RespostaApi<ContaReceberVinculoResponse>>> ObterPorOrdemServico(
        Guid ordemServicoId, CancellationToken ct)
    {
        var id = await sender.Send(new ObterContaReceberPorOrdemServicoQuery(ordemServicoId), ct);
        return Ok(RespostaApi<ContaReceberVinculoResponse>.Ok(new(id.HasValue, id)));
    }

    [HttpPost("contas-receber/{id:guid}/pagamentos"), Authorize(Policy = Permissoes.FinanceiroRegistrarPagamento)]
    public async Task<ActionResult<RespostaApi<ContaReceberDetalheResponse>>> RegistrarPagamento(Guid id,
        RegistrarPagamentoRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new RegistrarPagamentoCommand(id,
            (FormaPagamento)(int)request.FormaPagamento, request.Valor, request.Taxa,
            request.NumeroParcelas, request.Observacao, request.RecebidoEmLocal), ct);
        return Ok(RespostaApi<ContaReceberDetalheResponse>.Ok(Mapear(resultado), "Pagamento registrado com sucesso."));
    }

    [HttpPost("contas-receber/{id:guid}/pagamentos/{pagamentoId:guid}/estornar"),
     Authorize(Policy = Permissoes.FinanceiroEstornarPagamento)]
    public async Task<ActionResult<RespostaApi<ContaReceberDetalheResponse>>> EstornarPagamento(Guid id,
        Guid pagamentoId, EstornarPagamentoRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new EstornarPagamentoCommand(id, pagamentoId, request.Motivo), ct);
        return Ok(RespostaApi<ContaReceberDetalheResponse>.Ok(Mapear(resultado), "Pagamento estornado com sucesso."));
    }

    [HttpPatch("contas-receber/{id:guid}/vencimento"), Authorize(Policy = Permissoes.FinanceiroEditar)]
    public async Task<ActionResult<RespostaApi<ContaReceberDetalheResponse>>> AlterarVencimento(Guid id,
        AlterarVencimentoRequest request, CancellationToken ct)
    {
        var resultado = await sender.Send(new AlterarVencimentoContaReceberCommand(id, request.DataVencimento), ct);
        return Ok(RespostaApi<ContaReceberDetalheResponse>.Ok(Mapear(resultado), "Vencimento atualizado com sucesso."));
    }

    private static ContaReceberDetalheResponse Mapear(ContaReceberDetalheVisualizacao resultado)
    {
        var conta = resultado.Conta;
        string Nome(Guid id) => resultado.Usuarios.TryGetValue(id, out var nome) ? nome : "Usuário Detara";
        return new(conta.Id, conta.OrdemServicoId, conta.OrdemServicoCodigoSnapshot, conta.ClienteId,
            conta.ClienteNomeSnapshot, conta.VeiculoId, conta.VeiculoDescricaoSnapshot,
            conta.VeiculoPlacaSnapshot, conta.SubtotalAutorizado, conta.DescontoAutorizado,
            conta.AcrescimoAutorizado, conta.ValorOriginal, conta.ValorRecebido, conta.ValorEmAberto,
            conta.DataCompetencia, conta.DataVencimento, (StatusContaReceberContrato)(int)conta.Status,
            conta.EstaVencidaEm(resultado.HojeLocal), resultado.FusoHorario, conta.CriadoEmUtc,
            conta.AtualizadoEmUtc, conta.Pagamentos.OrderByDescending(item => item.RecebidoEmUtc)
                .Select(item => new PagamentoResponse(item.Id, (FormaPagamentoContrato)(int)item.FormaPagamento,
                    item.Valor, item.Taxa, item.ValorLiquido, item.NumeroParcelas, item.Observacao,
                    item.RecebidoEmUtc, item.RegistradoPorUsuarioId, Nome(item.RegistradoPorUsuarioId),
                    item.RegistradoEmUtc, (StatusPagamentoContrato)(int)item.Status, item.EstornadoEmUtc,
                    item.EstornadoPorUsuarioId, item.EstornadoPorUsuarioId.HasValue
                        ? Nome(item.EstornadoPorUsuarioId.Value) : null, item.MotivoEstorno)).ToArray());
    }
}
