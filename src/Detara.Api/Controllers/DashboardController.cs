using Detara.Application.Dashboard;
using Detara.Application.Agenda;
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
    public async Task<ActionResult<RespostaApi<DashboardOperacionalResponse>>> Obter(
        CancellationToken cancellationToken)
    {
        var permissoes = new PermissoesDashboardOperacional(
            await PodeAsync(Permissoes.AgendaVisualizar),
            await PodeAsync(Permissoes.OrdemServicoVisualizar),
            await PodeAsync(Permissoes.OrcamentosVisualizar),
            await PodeAsync(Permissoes.FinanceiroVisualizar));
        var resultado = await sender.Send(
            new ObterDashboardOperacionalQuery(permissoes),
            cancellationToken);

        DashboardAgendaResponse? agenda = resultado.Agenda is null
            ? null
            : new(
                resultado.Agenda.AgendamentosHoje,
                resultado.Agenda.ConcluidosHoje,
                resultado.Agenda.Itens.Select(item => new DashboardAgendamentoResponse(
                    item.Id,
                    conversor.ParaLocal(item.InicioUtc, resultado.FusoHorario),
                    item.ClienteNome,
                    item.VeiculoDescricao,
                    item.VeiculoPlaca,
                    item.ItemPrincipal,
                    (StatusAgendamentoContrato)(int)item.Status)).ToArray());
        DashboardAtendimentoResponse? atendimento = permissoes.PodeVerOrdensServico
            ? new(
                resultado.Atendimento?.OrdensEmExecucao ?? 0,
                resultado.Atendimento?.OrdensAguardandoRetirada ?? 0)
            : null;
        DashboardOrcamentosResponse? orcamentos = permissoes.PodeVerOrcamentos
            ? new(resultado.Atendimento?.OrcamentosEmAberto ?? 0)
            : null;
        DashboardFinanceiroResponse? financeiro = resultado.Financeiro is null
            ? null
            : new(
                resultado.InicioPeriodoFinanceiro,
                resultado.FimPeriodoFinanceiro,
                resultado.Financeiro.RecebidoBruto,
                resultado.Financeiro.Taxas,
                resultado.Financeiro.RecebidoBruto - resultado.Financeiro.Taxas,
                resultado.Financeiro.ContasPendentes,
                resultado.Financeiro.ValorPendente);

        return Ok(RespostaApi<DashboardOperacionalResponse>.Ok(new(
            resultado.DataReferencia,
            resultado.AtualizadoEmLocal,
            resultado.FusoHorario,
            agenda,
            atendimento,
            orcamentos,
            financeiro)));
    }

    private async Task<bool> PodeAsync(string permissao) =>
        (await authorizationService.AuthorizeAsync(User, permissao)).Succeeded;

}
