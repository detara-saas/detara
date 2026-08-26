using Detara.Application.Agenda;
using Detara.Application.Dashboard;
using Detara.Contracts.Agenda;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Dashboard;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(
    ISender sender,
    IAuthorizationService authorizationService,
    IConversorFusoHorario conversor) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<RespostaApi<DashboardExecutivoResponse>>> Obter(
        [FromQuery] PeriodoDashboardContrato periodo = PeriodoDashboardContrato.EsteMes,
        CancellationToken cancellationToken = default)
    {
        var permissoes = new PermissoesDashboardOperacional(
            await PodeAsync(Permissoes.AgendaVisualizar),
            await PodeAsync(Permissoes.OrdemServicoVisualizar),
            await PodeAsync(Permissoes.OrcamentosVisualizar),
            await PodeAsync(Permissoes.FinanceiroVisualizar));
        var resultado = await sender.Send(
            new ObterDashboardOperacionalQuery((PeriodoDashboard)(int)periodo, permissoes),
            cancellationToken);

        var response = new DashboardExecutivoResponse(
            resultado.DataReferencia,
            resultado.AtualizadoEmLocal,
            resultado.FusoHorario,
            new(
                (PeriodoDashboardContrato)(int)resultado.Periodo.Periodo,
                resultado.Periodo.Inicio,
                resultado.Periodo.Fim,
                (GranularidadeDashboardContrato)(int)resultado.Periodo.Granularidade),
            new(
                resultado.Resumo.AgendamentosHoje,
                resultado.Resumo.AgendamentosConcluidosHoje,
                resultado.Resumo.OrdensEmExecucao,
                resultado.Resumo.OrdensAguardandoRetirada,
                resultado.Resumo.ReceitaLiquida,
                resultado.Resumo.VariacaoReceitaPercentual),
            resultado.Financeiro is null ? null : new(
                resultado.Financeiro.RecebidoBruto,
                resultado.Financeiro.Taxas,
                resultado.Financeiro.ReceitaLiquida,
                resultado.Financeiro.TicketMedio,
                resultado.Financeiro.ContasPendentes,
                resultado.Financeiro.ValorEmAberto,
                resultado.Financeiro.ReceitaAoLongoPeriodo
                    .Select(item => new DashboardReceitaPontoResponse(item.Data, item.ReceitaLiquida))
                    .ToArray()),
            MapearOperacional(resultado),
            MapearComercial(resultado),
            new(
                resultado.Atividade.Itens.Select(item => new DashboardAtividadeItemResponse(
                    (TipoAtividadeDashboardContrato)(int)item.Tipo,
                    item.EntidadeId,
                    conversor.ParaLocal(item.DataUtc, resultado.FusoHorario),
                    item.Descricao,
                    ObterDestino(item.Tipo, item.EntidadeId))).ToArray(),
                resultado.Atividade.Atencoes.Select(item => new DashboardAtencaoItemResponse(
                    (TipoAtencaoDashboardContrato)(int)item.Tipo,
                    item.Quantidade,
                    item.Valor)).ToArray()));

        return Ok(RespostaApi<DashboardExecutivoResponse>.Ok(response));
    }

    private DashboardOperacionalResponse? MapearOperacional(DashboardExecutivoResultado resultado)
    {
        if (resultado.Operacional is null) return null;
        var item = resultado.Operacional;
        return new(
            item.ServicosRealizados,
            item.VeiculosEntregues,
            item.ClientesAtendidos,
            item.AtendimentosAtrasados,
            new(item.Fluxo.Agenda, item.Fluxo.ClienteChegou, item.Fluxo.EmExecucao,
                item.Fluxo.AguardandoRetirada, item.Fluxo.Concluido),
            item.AgendaHoje?.Select(agenda => new DashboardAgendamentoResponse(
                agenda.Id,
                conversor.ParaLocal(agenda.InicioUtc, resultado.FusoHorario),
                agenda.ClienteNome,
                agenda.VeiculoDescricao,
                agenda.VeiculoPlaca,
                agenda.ItemPrincipal,
                (StatusAgendamentoContrato)(int)agenda.Status)).ToArray());
    }

    private static DashboardComercialResponse? MapearComercial(DashboardExecutivoResultado resultado)
    {
        if (resultado.Comercial is null) return null;
        var item = resultado.Comercial;
        return new(
            item.OrcamentosCriados,
            item.OrcamentosEnviados,
            item.OrcamentosAprovados,
            item.OrcamentosRecusados,
            item.OrcamentosAguardandoAprovacao,
            item.TaxaConversao,
            item.ServicosMaisRealizados?.Select(servico => new DashboardServicoRankingResponse(
                servico.Nome, servico.Quantidade, servico.Percentual)).ToArray());
    }

    private static string ObterDestino(TipoAtividadeDashboard tipo, Guid id) => tipo switch
    {
        TipoAtividadeDashboard.AgendamentoCriado => $"/agenda/{id}",
        TipoAtividadeDashboard.OrcamentoAprovado => $"/orcamentos/{id}",
        TipoAtividadeDashboard.PagamentoRecebido => $"/financeiro/contas-receber/{id}",
        _ => $"/ordens-servico/{id}"
    };

    private async Task<bool> PodeAsync(string permissao) =>
        (await authorizationService.AuthorizeAsync(User, permissao)).Succeeded;
}
