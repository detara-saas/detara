using Detara.Application.Agenda;
using Detara.Application.Dashboard;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Financeiro;

internal sealed class FinanceiroDashboardConsulta(
    DetaraDbContext db,
    IConversorFusoHorario conversor)
    : IFinanceiroDashboardConsulta
{
    public async Task<DashboardFinanceiroConsultaResultado> ObterAsync(
        Guid empresaId,
        DashboardPeriodoDto periodo,
        string fusoHorario,
        int limiteAtividades,
        CancellationToken cancellationToken)
    {
        var pagamentosPeriodo = await db.Pagamentos
            .AsNoTracking()
            .Where(item =>
                item.EmpresaId == empresaId &&
                item.Status == StatusPagamento.Confirmado &&
                item.RecebidoEmUtc >= periodo.InicioUtc &&
                item.RecebidoEmUtc < periodo.FimExclusivoUtc)
            .Select(item => new
            {
                item.ContaReceberId,
                item.Valor,
                item.Taxa,
                item.RecebidoEmUtc,
                item.ContaReceber.VeiculoDescricaoSnapshot
            })
            .ToArrayAsync(cancellationToken);

        var pagamentosAnteriores = await db.Pagamentos
            .AsNoTracking()
            .Where(item =>
                item.EmpresaId == empresaId &&
                item.Status == StatusPagamento.Confirmado &&
                item.RecebidoEmUtc >= periodo.InicioAnteriorUtc &&
                item.RecebidoEmUtc < periodo.FimAnteriorExclusivoUtc)
            .Select(item => new { item.Valor, item.Taxa })
            .ToArrayAsync(cancellationToken);

        var contasPeriodo = await db.ContasReceber
            .AsNoTracking()
            .Where(item =>
                item.EmpresaId == empresaId &&
                item.DataCompetencia >= periodo.Inicio &&
                item.DataCompetencia <= periodo.Fim)
            .Select(item => item.ValorOriginal)
            .ToArrayAsync(cancellationToken);

        var contasPendentes = await db.ContasReceber
            .AsNoTracking()
            .Where(item =>
                item.EmpresaId == empresaId &&
                item.Status != StatusContaReceber.Pago)
            .Select(item => new { item.ValorOriginal, item.ValorRecebido })
            .ToArrayAsync(cancellationToken);

        var recebidoBruto = pagamentosPeriodo.Sum(item => item.Valor);
        var taxas = pagamentosPeriodo.Sum(item => item.Taxa);
        var receitaAnterior = pagamentosAnteriores.Sum(item => item.Valor - item.Taxa);
        var receita = pagamentosPeriodo
            .GroupBy(item => DateOnly.FromDateTime(conversor.ParaLocal(item.RecebidoEmUtc, fusoHorario)))
            .Select(grupo => new DashboardReceitaPontoConsulta(
                grupo.Key,
                grupo.Sum(item => item.Valor - item.Taxa)))
            .OrderBy(item => item.Data)
            .ToArray();
        var atividades = pagamentosPeriodo
            .OrderByDescending(item => item.RecebidoEmUtc)
            .Take(limiteAtividades)
            .Select(item => new DashboardAtividadeItemDto(
                TipoAtividadeDashboard.PagamentoRecebido,
                item.ContaReceberId,
                item.RecebidoEmUtc,
                item.VeiculoDescricaoSnapshot))
            .ToArray();

        return new(
            recebidoBruto,
            taxas,
            receitaAnterior,
            contasPeriodo.Length == 0 ? 0 : decimal.Round(contasPeriodo.Average(), 2),
            contasPendentes.Length,
            contasPendentes.Sum(item => item.ValorOriginal - item.ValorRecebido),
            receita,
            atividades);
    }
}
