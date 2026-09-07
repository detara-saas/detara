using Detara.Application.Abstracoes;
using Detara.Application.Financeiro;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Financeiro;

internal sealed class DespesasRepositorio(DetaraDbContext db) : IDespesasRepositorio
{
    public async Task<DespesasResultado> ListarAsync(ListarDespesasQuery f, DateOnly hoje, CancellationToken ct)
    {
        var competencia = f.Competencia ?? new DateOnly(hoje.Year, hoje.Month, 1);
        var periodo = db.ContasPagar.AsNoTracking().Where(x => x.Competencia == competencia);
        ResumoDespesasResultado resumo;
        if (db.Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var valores = await periodo.Select(x => new { x.Status, x.Valor, x.ValorPago, x.DataVencimento }).ToArrayAsync(ct);
            resumo = new(valores.Where(x => x.Status != StatusContaPagar.Cancelado).Sum(x => x.Valor),
                valores.Where(x => x.Status == StatusContaPagar.Pago).Sum(x => x.ValorPago ?? 0),
                valores.Where(x => x.Status == StatusContaPagar.Pendente && x.DataVencimento >= hoje).Sum(x => x.Valor),
                valores.Where(x => x.Status == StatusContaPagar.Pendente && x.DataVencimento < hoje).Sum(x => x.Valor));
        }
        else
        {
            resumo = new(await periodo.Where(x => x.Status != StatusContaPagar.Cancelado).SumAsync(x => (decimal?)x.Valor, ct) ?? 0,
                await periodo.Where(x => x.Status == StatusContaPagar.Pago).SumAsync(x => x.ValorPago, ct) ?? 0,
                await periodo.Where(x => x.Status == StatusContaPagar.Pendente && x.DataVencimento >= hoje).SumAsync(x => (decimal?)x.Valor, ct) ?? 0,
                await periodo.Where(x => x.Status == StatusContaPagar.Pendente && x.DataVencimento < hoje).SumAsync(x => (decimal?)x.Valor, ct) ?? 0);
        }
        var query = periodo;
        if (f.Status == FiltroStatusDespesa.Vencido) query = query.Where(x => x.Status == StatusContaPagar.Pendente && x.DataVencimento < hoje);
        else if (f.Status.HasValue) query = query.Where(x => x.Status == (StatusContaPagar)(int)f.Status.Value);
        if (f.CategoriaId.HasValue) query = query.Where(x => x.CategoriaDespesaId == f.CategoriaId);
        if (f.Origem.HasValue) query = query.Where(x => x.Origem == f.Origem);
        if (!string.IsNullOrWhiteSpace(f.Pesquisa))
        { var termo = f.Pesquisa.Trim(); query = query.Where(x => x.Descricao.Contains(termo) || x.Fornecedor != null && x.Fornecedor.Contains(termo)); }
        var total = await query.CountAsync(ct);
        var itens = await query.OrderBy(x => x.Status == StatusContaPagar.Pendente ? 0 : 1).ThenBy(x => x.DataVencimento).ThenBy(x => x.Id)
            .Skip((f.Pagina - 1) * f.TamanhoPagina).Take(f.TamanhoPagina)
            .Select(x => new DespesaListaResultado(x.Id, x.Descricao, x.CategoriaDespesaId, x.CategoriaNomeSnapshot,
                x.Origem, x.DespesaRecorrenteId, x.Competencia, x.DataVencimento, x.Valor, x.Status,
                x.Status == StatusContaPagar.Pendente && x.DataVencimento < hoje, x.DataPagamento, x.ValorPago,
                x.Fornecedor, x.Observacao, x.Versao)).ToArrayAsync(ct);
        return new(new(itens, f.Pagina, f.TamanhoPagina, total), resumo, competencia, hoje);
    }
    public Task<ContaPagar?> ObterContaAsync(Guid id, CancellationToken ct) => db.ContasPagar.Include(x => x.Pagamentos).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<DespesaRecorrente?> ObterRecorrenciaAsync(Guid id, CancellationToken ct) => db.DespesasRecorrentes.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<CategoriaDespesa?> ObterCategoriaAsync(Guid id, CancellationToken ct) => db.CategoriasDespesa.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> CategoriaPossuiRecorrenciasAtivasAsync(Guid id, CancellationToken ct) =>
        db.DespesasRecorrentes.AnyAsync(x => x.CategoriaDespesaId == id && x.EhAtivo, ct);
    public async Task<PaginacaoResultado<RecorrenciaListaResultado>> ListarRecorrenciasAsync(int pagina, int tamanho, CancellationToken ct)
    {
        var query = from r in db.DespesasRecorrentes.AsNoTracking()
                    join c in db.CategoriasDespesa.AsNoTracking() on new { r.EmpresaId, Id = r.CategoriaDespesaId } equals new { c.EmpresaId, c.Id }
                    select new { Regra = r, Categoria = c.Nome };
        var total = await query.CountAsync(ct);
        return new(await query.OrderByDescending(x => x.Regra.EhAtivo).ThenBy(x => x.Regra.Descricao).ThenBy(x => x.Regra.Id)
            .Skip((pagina - 1) * tamanho).Take(tamanho)
            .Select(x => new RecorrenciaListaResultado(x.Regra.Id, x.Regra.Descricao, x.Regra.CategoriaDespesaId,
                x.Categoria, x.Regra.Valor, x.Regra.DiaVencimento, x.Regra.CompetenciaInicial,
                x.Regra.CompetenciaFinal, x.Regra.ProximaCompetencia, x.Regra.EhAtivo,
                x.Regra.Fornecedor, x.Regra.Observacao, x.Regra.Versao)).ToArrayAsync(ct), pagina, tamanho, total);
    }
    public async Task<IReadOnlyCollection<CategoriaDespesaResultado>> ListarCategoriasAsync(CancellationToken ct) =>
        await db.CategoriasDespesa.AsNoTracking().OrderBy(x => x.Nome)
            .Select(x => new CategoriaDespesaResultado(x.Id, x.Nome, x.EhAtivo, x.Versao)).ToArrayAsync(ct);
    public void Adicionar(ContaPagar x) => db.ContasPagar.Add(x);
    public void Adicionar(DespesaRecorrente x) => db.DespesasRecorrentes.Add(x);
    public void Adicionar(CategoriaDespesa x) => db.CategoriasDespesa.Add(x);
    public void Adicionar(PagamentoContaPagar x) => db.PagamentosContasPagar.Add(x);
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
