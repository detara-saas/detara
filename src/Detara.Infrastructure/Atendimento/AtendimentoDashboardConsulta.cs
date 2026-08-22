using Detara.Application.Dashboard;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Atendimento;

internal sealed class AtendimentoDashboardConsulta(DetaraDbContext db)
    : IAtendimentoDashboardConsulta
{
    public async Task<DashboardAtendimentoResultado> ObterAsync(
        Guid empresaId,
        DateOnly hojeLocal,
        bool consultarOrdensServico,
        bool consultarOrcamentos,
        CancellationToken cancellationToken)
    {
        var emExecucao = 0;
        var aguardandoRetirada = 0;
        var orcamentosEmAberto = 0;

        if (consultarOrdensServico)
        {
            var statusOrdens = await db.OrdensServico
                .AsNoTracking()
                .Where(ordem =>
                    ordem.EmpresaId == empresaId &&
                    (ordem.Status == StatusOrdemServico.EmExecucao ||
                     ordem.Status == StatusOrdemServico.AguardandoRetirada))
                .GroupBy(ordem => ordem.Status)
                .Select(grupo => new { Status = grupo.Key, Quantidade = grupo.Count() })
                .ToArrayAsync(cancellationToken);
            emExecucao = statusOrdens
                .Where(item => item.Status == StatusOrdemServico.EmExecucao)
                .Sum(item => item.Quantidade);
            aguardandoRetirada = statusOrdens
                .Where(item => item.Status == StatusOrdemServico.AguardandoRetirada)
                .Sum(item => item.Quantidade);
        }

        if (consultarOrcamentos)
        {
            orcamentosEmAberto = await db.Orcamentos
                .AsNoTracking()
                .CountAsync(orcamento =>
                    orcamento.EmpresaId == empresaId &&
                    (orcamento.Status == StatusOrcamento.Rascunho ||
                     orcamento.Status == StatusOrcamento.Emitido &&
                     orcamento.ValidoAte >= hojeLocal),
                    cancellationToken);
        }

        return new(emExecucao, aguardandoRetirada, orcamentosEmAberto);
    }
}
