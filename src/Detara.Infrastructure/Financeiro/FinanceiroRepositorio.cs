using Detara.Application.Abstracoes;
using Detara.Application.Financeiro;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Financeiro;

internal sealed class FinanceiroRepositorio(DetaraDbContext db) : IFinanceiroRepositorio
{
    public async Task<PaginacaoResultado<ContaReceberListaResultado>> ListarAsync(
        FiltroContasReceber filtro, CancellationToken ct)
    {
        var query = db.ContasReceber.AsNoTracking();
        if (filtro.Status.HasValue) query = query.Where(item => item.Status == filtro.Status);
        if (filtro.Vencida == true)
            query = query.Where(item => item.Status != StatusContaReceber.Pago && item.DataVencimento < filtro.HojeLocal);
        else if (filtro.Vencida == false)
            query = query.Where(item => item.Status == StatusContaReceber.Pago || item.DataVencimento >= filtro.HojeLocal);
        if (filtro.CompetenciaInicial.HasValue)
            query = query.Where(item => item.DataCompetencia >= filtro.CompetenciaInicial);
        if (filtro.CompetenciaFinal.HasValue)
            query = query.Where(item => item.DataCompetencia <= filtro.CompetenciaFinal);
        if (!string.IsNullOrWhiteSpace(filtro.Pesquisa))
        {
            var termo = filtro.Pesquisa.Trim();
            var placa = new string(termo.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            query = query.Where(item => item.OrdemServicoCodigoSnapshot.Contains(termo) ||
                item.ClienteNomeSnapshot.Contains(termo) || item.VeiculoDescricaoSnapshot.Contains(termo) ||
                item.VeiculoPlacaSnapshot.Contains(placa));
        }

        var total = await query.CountAsync(ct);
        var itens = await query.OrderByDescending(item => item.DataCompetencia)
            .ThenByDescending(item => item.CriadoEmUtc)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina).Take(filtro.TamanhoPagina)
            .Select(item => new ContaReceberListaResultado(item.Id, item.OrdemServicoId,
                item.OrdemServicoCodigoSnapshot, item.ClienteNomeSnapshot, item.VeiculoDescricaoSnapshot,
                item.VeiculoPlacaSnapshot, item.DataCompetencia, item.DataVencimento,
                item.ValorOriginal, item.ValorRecebido, item.Status,
                item.Status != StatusContaReceber.Pago && item.DataVencimento < filtro.HojeLocal)).ToArrayAsync(ct);
        return new(itens, filtro.Pagina, filtro.TamanhoPagina, total);
    }

    public Task<ContaReceber?> ObterAsync(Guid id, bool paraAlteracao, CancellationToken ct)
    {
        var query = db.ContasReceber.Include(item => item.Pagamentos).Where(item => item.Id == id);
        return (paraAlteracao ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
    }

    public Task<bool> ExistePorOrdemServicoAsync(Guid ordemServicoId, CancellationToken ct)
    {
        if (db.ContasReceber.Local.Any(item => item.OrdemServicoId == ordemServicoId)) return Task.FromResult(true);
        return db.ContasReceber.AnyAsync(item => item.OrdemServicoId == ordemServicoId, ct);
    }

    public Task<Guid?> ObterIdPorOrdemServicoAsync(Guid ordemServicoId, CancellationToken ct) =>
        db.ContasReceber.AsNoTracking().Where(item => item.OrdemServicoId == ordemServicoId)
            .Select(item => (Guid?)item.Id).SingleOrDefaultAsync(ct);

    public async Task<ResumoFinanceiroResultado> ObterResumoAsync(DateOnly inicio, DateOnly fim,
        DateTime inicioUtc, DateTime fimExclusivoUtc, DateOnly hojeLocal, CancellationToken ct)
    {
        var contasPeriodo = db.ContasReceber.AsNoTracking()
            .Where(item => item.DataCompetencia >= inicio && item.DataCompetencia <= fim);
        var pagamentosPeriodo = db.Pagamentos.AsNoTracking().Where(item =>
            item.Status == StatusPagamento.Confirmado && item.RecebidoEmUtc >= inicioUtc && item.RecebidoEmUtc < fimExclusivoUtc);

        if (string.Equals(db.Database.ProviderName, "Microsoft.EntityFrameworkCore.Sqlite",
            StringComparison.Ordinal))
        {
            var contas = await contasPeriodo.Select(x => x.ValorOriginal).ToArrayAsync(ct);
            var pagamentos = await pagamentosPeriodo.Select(x => new { x.FormaPagamento, x.Valor, x.Taxa }).ToArrayAsync(ct);
            var abertas = await db.ContasReceber.AsNoTracking().Where(x => x.Status != StatusContaReceber.Pago)
                .Select(x => new { x.ValorOriginal, x.ValorRecebido, x.DataVencimento }).ToArrayAsync(ct);
            return new(contas.Sum(), contas.Length, pagamentos.Sum(x => x.Valor), pagamentos.Sum(x => x.Taxa),
                abertas.Sum(x => x.ValorOriginal - x.ValorRecebido),
                abertas.Where(x => x.DataVencimento < hojeLocal).Sum(x => x.ValorOriginal - x.ValorRecebido),
                pagamentos.GroupBy(x => x.FormaPagamento).Select(x => new FormaPagamentoResumo(x.Key,
                    x.Sum(item => item.Valor), x.Count())).ToArray());
        }

        var faturado = await contasPeriodo.SumAsync(item => (decimal?)item.ValorOriginal, ct) ?? 0;
        var quantidadeContas = await contasPeriodo.CountAsync(ct);
        var recebido = await pagamentosPeriodo.SumAsync(item => (decimal?)item.Valor, ct) ?? 0;
        var taxas = await pagamentosPeriodo.SumAsync(item => (decimal?)item.Taxa, ct) ?? 0;
        var abertasQuery = db.ContasReceber.AsNoTracking().Where(item => item.Status != StatusContaReceber.Pago);
        var emAberto = await abertasQuery.SumAsync(item => (decimal?)(item.ValorOriginal - item.ValorRecebido), ct) ?? 0;
        var vencido = await abertasQuery.Where(item => item.DataVencimento < hojeLocal)
            .SumAsync(item => (decimal?)(item.ValorOriginal - item.ValorRecebido), ct) ?? 0;
        var formas = await pagamentosPeriodo.GroupBy(item => item.FormaPagamento)
            .Select(grupo => new FormaPagamentoResumo(grupo.Key, grupo.Sum(item => item.Valor), grupo.Count()))
            .ToArrayAsync(ct);
        return new(faturado, quantidadeContas, recebido, taxas, emAberto, vencido, formas);
    }

    public void Adicionar(ContaReceber conta) => db.ContasReceber.Add(conta);
    public void AdicionarPagamento(Pagamento pagamento) => db.Pagamentos.Add(pagamento);
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}

internal sealed class PlataformaFinanceiroConsulta(DetaraDbContext db) : IPlataformaFinanceiroConsulta
{
    public Task<string?> ObterFusoHorarioAsync(Guid empresaId, CancellationToken ct) => db.Empresas.AsNoTracking()
        .Where(item => item.Id == empresaId).Select(item => item.FusoHorario).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> ObterNomesUsuariosAsync(Guid empresaId,
        IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct) => await db.Usuarios.IgnoreQueryFilters()
        .AsNoTracking().Where(item => item.EmpresaId == empresaId && usuarioIds.Contains(item.Id))
        .ToDictionaryAsync(item => item.Id, item => item.Nome, ct);
}
