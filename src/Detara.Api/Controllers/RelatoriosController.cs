using Detara.Application.Relatorios;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Relatorios;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Detara.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/relatorios")]
public sealed class RelatoriosController(ISender sender, IAuthorizationService autorizacao) : ControllerBase
{
    [HttpGet("{perspectiva}")]
    public async Task<ActionResult<RespostaApi<RelatorioResponse>>> Obter(PerspectivaRelatorioContrato perspectiva,
        [FromQuery] PeriodoRelatorioContrato periodo = PeriodoRelatorioContrato.EsteMes,
        [FromQuery] DateOnly? inicio = null, [FromQuery] DateOnly? fim = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(perspectiva)) return BadRequest();
        var p = new PermissoesRelatorios(await Pode(Permissoes.FinanceiroVisualizar), await Pode(Permissoes.OrdemServicoVisualizar),
            await Pode(Permissoes.OrcamentosVisualizar), await Pode(Permissoes.ClientesVisualizar), await Pode(Permissoes.AgendaVisualizar));
        var permitido = perspectiva switch
        {
            PerspectivaRelatorioContrato.Geral => p.Financeiro || p.Ordens,
            PerspectivaRelatorioContrato.Financeiro => p.Financeiro,
            PerspectivaRelatorioContrato.Clientes => p.Clientes && p.Ordens,
            PerspectivaRelatorioContrato.Servicos => p.Ordens || p.Orcamentos,
            PerspectivaRelatorioContrato.Operacao => p.Agenda || p.Ordens,
            _ => false
        };
        if (!permitido) return Forbid();
        var r = await sender.Send(new ObterRelatorioQuery((PerspectivaRelatorio)(int)perspectiva,
            (PeriodoRelatorio)(int)periodo, inicio, fim, p), cancellationToken);
        var f = r.Financeiro;
        var a = r.Atendimento;
        return Ok(RespostaApi<RelatorioResponse>.Ok(new(r.Periodo.Atual.Inicio, r.Periodo.Atual.Fim,
            r.Periodo.Anterior.Inicio, r.Periodo.Anterior.Fim, r.Periodo.Fuso,
            f is null ? null : new(f.Receita, f.Despesas, f.Resultado, f.Recebimentos, f.Pagamentos, f.AReceber, f.APagar, f.Vencido, Dias(f.Serie), Ranking(f.Categorias)),
            a is null ? null : new(a.Concluidos, a.Valor, a.Ticket, a.Clientes, a.Novos, a.Recorrentes, a.QuantidadeServicos,
                Ranking(a.ServicosQuantidade), Ranking(a.ServicosValor), Ranking(a.ClientesConsumo), Ranking(a.ClientesFrequencia), Dias(a.Dias)),
            r.Orcamentos is null ? null : new(r.Orcamentos.Criados, r.Orcamentos.Aprovados, r.Orcamentos.Recusados, r.Orcamentos.Conversao),
            r.Agenda is null ? null : new(r.Agenda.Agendamentos, r.Agenda.Cancelados, r.Agenda.NaoCompareceu),
            r.VariacaoReceita, r.VariacaoTicket, r.Insights.Select(x => new InsightRelatorioResponse(x.Tipo, x.Nome, x.Valor, x.Percentual)).ToArray())));
    }

    private async Task<bool> Pode(string policy) => (await autorizacao.AuthorizeAsync(User, policy)).Succeeded;
    private static RankingRelatorioResponse[] Ranking(IReadOnlyList<RankingRelatorio> itens) => itens.Select(x => new RankingRelatorioResponse(x.Id, x.Nome, x.Quantidade, x.Valor)).ToArray();
    private static DiaRelatorioResponse[] Dias(IReadOnlyList<DiaRelatorio> itens) => itens.Select(x => new DiaRelatorioResponse(x.Data, x.Receita, x.Despesa, x.Atendimentos)).ToArray();
}
