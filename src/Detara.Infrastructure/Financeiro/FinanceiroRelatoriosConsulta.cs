using Detara.Application.Relatorios;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Financeiro;

internal sealed class FinanceiroRelatoriosConsulta(DetaraDbContext db) : IFinanceiroRelatoriosConsulta
{
    public async Task<FinanceiroRelatorio> ObterAsync(Guid empresaId, IntervaloRelatorio p, DateOnly hoje,
        string fuso, bool detalhar, CancellationToken ct)
    {
        var receitas = db.Pagamentos.AsNoTracking().Where(x => x.EmpresaId == empresaId &&
            x.Status == StatusPagamento.Confirmado && x.RecebidoEmUtc >= p.InicioUtc && x.RecebidoEmUtc < p.FimExclusivoUtc);
        // O snapshot da conta reflete apenas o pagamento vigente; estorno limpa ValorPago/DataPagamento.
        var despesas = db.ContasPagar.AsNoTracking().Where(x => x.EmpresaId == empresaId &&
            x.Status == StatusContaPagar.Pago && x.DataPagamento >= p.Inicio && x.DataPagamento <= p.Fim);
        var r = await receitas.GroupBy(_ => 1).Select(g => new { Valor = g.Sum(x => x.Valor), Quantidade = g.Count() }).SingleOrDefaultAsync(ct);
        var d = await despesas.GroupBy(_ => 1).Select(g => new { Valor = g.Sum(x => x.ValorPago ?? 0), Quantidade = g.Count() }).SingleOrDefaultAsync(ct);
        if (!detalhar) return new(r?.Valor ?? 0, d?.Valor ?? 0, r?.Quantidade ?? 0, d?.Quantidade ?? 0, 0, 0, 0, [], []);
        var receber = await db.ContasReceber.AsNoTracking().Where(x => x.EmpresaId == empresaId &&
            x.DataVencimento >= p.Inicio && x.DataVencimento <= p.Fim && x.Status != StatusContaReceber.Pago)
            .SumAsync(x => (decimal?)(x.ValorOriginal - x.ValorRecebido), ct) ?? 0;
        var pendentes = db.ContasPagar.AsNoTracking().Where(x => x.EmpresaId == empresaId && x.Status == StatusContaPagar.Pendente &&
            x.DataVencimento >= p.Inicio && x.DataVencimento <= p.Fim);
        var pagar = await pendentes.SumAsync(x => (decimal?)x.Valor, ct) ?? 0;
        var vencido = await pendentes.Where(x => x.DataVencimento < hoje).SumAsync(x => (decimal?)x.Valor, ct) ?? 0;
        var categorias = await despesas.GroupBy(x => x.CategoriaDespesaId)
            .Select(g => new { Id = g.Key, Nome = g.Max(x => x.CategoriaNomeSnapshot)!, Quantidade = g.Count(), Valor = g.Sum(x => x.ValorPago ?? 0) })
            .OrderByDescending(x => x.Valor).ThenBy(x => x.Id).Take(10).ToArrayAsync(ct);
        var ranking = categorias.Select(x => new RankingRelatorio(x.Id, x.Nome, x.Quantidade, x.Valor)).ToList();
        var outros = (d?.Valor ?? 0) - ranking.Sum(x => x.Valor);
        if (outros > 0) ranking.Add(new(null, "Outras categorias", 0, outros));
        var serieReceita = await SeriesRelatorio.AgregarAsync(receitas.Select(x => new MovimentoRelatorio { Data = x.RecebidoEmUtc, Valor = x.Valor, Quantidade = 1 }), db.Database.IsSqlServer(), fuso, ct);
        var serieDespesa = await despesas.GroupBy(x => x.DataPagamento!.Value)
            .Select(g => new { Data = g.Key, Valor = g.Sum(x => x.ValorPago ?? 0) }).ToArrayAsync(ct);
        var serie = serieReceita.Select(x => x with { Atendimentos = 0 })
            .Concat(serieDespesa.Select(x => new DiaRelatorio(x.Data, 0, x.Valor)))
            .GroupBy(x => x.Data).Select(g => new DiaRelatorio(g.Key, g.Sum(x => x.Receita), g.Sum(x => x.Despesa)))
            .OrderBy(x => x.Data).ToArray();
        return new(r?.Valor ?? 0, d?.Valor ?? 0, r?.Quantidade ?? 0, d?.Quantidade ?? 0, receber, pagar, vencido, serie, ranking);
    }
}
