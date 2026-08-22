using Detara.Application.Dashboard;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Financeiro;

internal sealed class FinanceiroDashboardConsulta(DetaraDbContext db)
    : IFinanceiroDashboardConsulta
{
    public async Task<DashboardFinanceiroResultado> ObterAsync(
        Guid empresaId,
        DateTime inicioPeriodoUtc,
        DateTime fimPeriodoExclusivoUtc,
        CancellationToken cancellationToken)
    {
        var pagamentos = db.Pagamentos
            .AsNoTracking()
            .Where(pagamento =>
                pagamento.EmpresaId == empresaId &&
                pagamento.Status == StatusPagamento.Confirmado &&
                pagamento.RecebidoEmUtc >= inicioPeriodoUtc &&
                pagamento.RecebidoEmUtc < fimPeriodoExclusivoUtc);
        var contasPendentes = db.ContasReceber
            .AsNoTracking()
            .Where(conta =>
                conta.EmpresaId == empresaId &&
                conta.Status != StatusContaReceber.Pago);

        decimal recebidoBruto;
        decimal taxas;
        int quantidadePendentes;
        decimal valorPendente;

        if (string.Equals(
            db.Database.ProviderName,
            "Microsoft.EntityFrameworkCore.Sqlite",
            StringComparison.Ordinal))
        {
            var pagamentosProjetados = await pagamentos
                .Select(pagamento => new { pagamento.Valor, pagamento.Taxa })
                .ToArrayAsync(cancellationToken);
            var contasProjetadas = await contasPendentes
                .Select(conta => new { conta.ValorOriginal, conta.ValorRecebido })
                .ToArrayAsync(cancellationToken);
            recebidoBruto = pagamentosProjetados.Sum(item => item.Valor);
            taxas = pagamentosProjetados.Sum(item => item.Taxa);
            quantidadePendentes = contasProjetadas.Length;
            valorPendente = contasProjetadas.Sum(item => item.ValorOriginal - item.ValorRecebido);
        }
        else
        {
            var resumoPagamentos = await pagamentos
                .GroupBy(_ => 1)
                .Select(grupo => new
                {
                    Recebido = grupo.Sum(item => item.Valor),
                    Taxas = grupo.Sum(item => item.Taxa)
                })
                .SingleOrDefaultAsync(cancellationToken);
            var resumoPendencias = await contasPendentes
                .GroupBy(_ => 1)
                .Select(grupo => new
                {
                    Quantidade = grupo.Count(),
                    Valor = grupo.Sum(item => item.ValorOriginal - item.ValorRecebido)
                })
                .SingleOrDefaultAsync(cancellationToken);
            recebidoBruto = resumoPagamentos?.Recebido ?? 0;
            taxas = resumoPagamentos?.Taxas ?? 0;
            quantidadePendentes = resumoPendencias?.Quantidade ?? 0;
            valorPendente = resumoPendencias?.Valor ?? 0;
        }

        return new(recebidoBruto, taxas, quantidadePendentes, valorPendente);
    }
}
