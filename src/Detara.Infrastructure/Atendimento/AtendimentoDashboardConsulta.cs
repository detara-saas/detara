using Detara.Application.Dashboard;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Atendimento;

internal sealed class AtendimentoDashboardConsulta(DetaraDbContext db)
    : IAtendimentoDashboardConsulta
{
    public async Task<DashboardAtendimentoConsultaResultado> ObterAsync(
        Guid empresaId,
        DashboardPeriodoDto periodo,
        DateOnly hojeLocal,
        bool consultarOrdensServico,
        bool consultarOrcamentos,
        int limiteRanking,
        int limiteAtividades,
        CancellationToken cancellationToken)
    {
        var ordensEmExecucao = 0;
        var ordensAguardandoRetirada = 0;
        var orcamentosAguardandoAprovacao = 0;
        var servicosRealizados = 0;
        var veiculosEntregues = 0;
        var clientesAtendidos = 0;
        var orcamentosCriados = 0;
        var orcamentosEnviados = 0;
        var orcamentosAprovados = 0;
        var orcamentosRecusados = 0;
        var clientesQueChegaram = 0;
        var ordensEmExecucaoPeriodo = 0;
        var ordensAguardandoPeriodo = 0;
        var ordensConcluidasPeriodo = 0;
        IReadOnlyCollection<DashboardServicoQuantidadeConsulta> ranking = [];
        var atividades = new List<DashboardAtividadeItemDto>();

        if (consultarOrdensServico)
        {
            var ordens = db.OrdensServico
                .AsNoTracking()
                .Where(ordem => ordem.EmpresaId == empresaId);
            var statusAtuais = await ordens
                .Where(ordem =>
                    ordem.Status == StatusOrdemServico.EmExecucao ||
                    ordem.Status == StatusOrdemServico.AguardandoRetirada)
                .GroupBy(ordem => ordem.Status)
                .Select(grupo => new { Status = grupo.Key, Quantidade = grupo.Count() })
                .ToArrayAsync(cancellationToken);
            ordensEmExecucao = statusAtuais
                .Where(item => item.Status == StatusOrdemServico.EmExecucao)
                .Sum(item => item.Quantidade);
            ordensAguardandoRetirada = statusAtuais
                .Where(item => item.Status == StatusOrdemServico.AguardandoRetirada)
                .Sum(item => item.Quantidade);

            var metricas = await ordens
                .GroupBy(_ => 1)
                .Select(grupo => new
                {
                    VeiculosEntregues = grupo.Count(ordem =>
                        ordem.ConcluidaEmUtc >= periodo.InicioUtc &&
                        ordem.ConcluidaEmUtc < periodo.FimExclusivoUtc),
                    ClientesQueChegaram = grupo.Count(ordem =>
                        ordem.CheckInEmUtc >= periodo.InicioUtc &&
                        ordem.CheckInEmUtc < periodo.FimExclusivoUtc),
                    EmExecucao = grupo.Count(ordem =>
                        ordem.IniciadaEmUtc >= periodo.InicioUtc &&
                        ordem.IniciadaEmUtc < periodo.FimExclusivoUtc),
                    Aguardando = grupo.Count(ordem =>
                        ordem.ExecucaoFinalizadaEmUtc >= periodo.InicioUtc &&
                        ordem.ExecucaoFinalizadaEmUtc < periodo.FimExclusivoUtc),
                    Concluidas = grupo.Count(ordem =>
                        ordem.ConcluidaEmUtc >= periodo.InicioUtc &&
                        ordem.ConcluidaEmUtc < periodo.FimExclusivoUtc)
                })
                .SingleOrDefaultAsync(cancellationToken);
            veiculosEntregues = metricas?.VeiculosEntregues ?? 0;
            clientesQueChegaram = metricas?.ClientesQueChegaram ?? 0;
            ordensEmExecucaoPeriodo = metricas?.EmExecucao ?? 0;
            ordensAguardandoPeriodo = metricas?.Aguardando ?? 0;
            ordensConcluidasPeriodo = metricas?.Concluidas ?? 0;

            clientesAtendidos = await ordens
                .Where(ordem =>
                    ordem.IniciadaEmUtc >= periodo.InicioUtc &&
                    ordem.IniciadaEmUtc < periodo.FimExclusivoUtc)
                .Select(ordem => ordem.ClienteId)
                .Distinct()
                .CountAsync(cancellationToken);

            var itensRealizados = db.OrdensServicoItens
                .AsNoTracking()
                .Where(item =>
                    item.EmpresaId == empresaId &&
                    item.OrdemServico.ExecucaoFinalizadaEmUtc >= periodo.InicioUtc &&
                    item.OrdemServico.ExecucaoFinalizadaEmUtc < periodo.FimExclusivoUtc &&
                    item.OrdemServico.Status != StatusOrdemServico.Cancelada);
            servicosRealizados = await itensRealizados.SumAsync(
                item => (int?)item.Quantidade,
                cancellationToken) ?? 0;
            var rankingBruto = await itensRealizados
                .GroupBy(item => item.NomeSnapshot)
                .Select(grupo => new
                {
                    Nome = grupo.Key,
                    Quantidade = grupo.Sum(item => item.Quantidade)
                })
                .OrderByDescending(item => item.Quantidade)
                .ThenBy(item => item.Nome)
                .Take(limiteRanking)
                .ToArrayAsync(cancellationToken);
            ranking = rankingBruto
                .Select(item => new DashboardServicoQuantidadeConsulta(item.Nome, item.Quantidade))
                .ToArray();

            atividades.AddRange(await ObterAtividadesOrdensAsync(
                ordens, periodo, limiteAtividades, cancellationToken));
        }

        if (consultarOrcamentos)
        {
            var orcamentos = db.Orcamentos
                .AsNoTracking()
                .Where(orcamento => orcamento.EmpresaId == empresaId);
            orcamentosAguardandoAprovacao = await orcamentos.CountAsync(
                orcamento =>
                    orcamento.Status == StatusOrcamento.Rascunho ||
                    orcamento.Status == StatusOrcamento.Emitido && orcamento.ValidoAte >= hojeLocal,
                cancellationToken);
            var funil = await orcamentos
                .Where(orcamento =>
                    orcamento.CriadoEmUtc >= periodo.InicioUtc &&
                    orcamento.CriadoEmUtc < periodo.FimExclusivoUtc)
                .GroupBy(_ => 1)
                .Select(grupo => new
                {
                    Criados = grupo.Count(),
                    Enviados = grupo.Count(item => item.EmitidoEmUtc.HasValue),
                    Aprovados = grupo.Count(item => item.AprovadoEmUtc.HasValue),
                    Recusados = grupo.Count(item => item.RecusadoEmUtc.HasValue)
                })
                .SingleOrDefaultAsync(cancellationToken);
            orcamentosCriados = funil?.Criados ?? 0;
            orcamentosEnviados = funil?.Enviados ?? 0;
            orcamentosAprovados = funil?.Aprovados ?? 0;
            orcamentosRecusados = funil?.Recusados ?? 0;

            atividades.AddRange(await orcamentos
                .Where(orcamento =>
                    orcamento.AprovadoEmUtc >= periodo.InicioUtc &&
                    orcamento.AprovadoEmUtc < periodo.FimExclusivoUtc)
                .OrderByDescending(orcamento => orcamento.AprovadoEmUtc)
                .Take(limiteAtividades)
                .Select(orcamento => new DashboardAtividadeItemDto(
                    TipoAtividadeDashboard.OrcamentoAprovado,
                    orcamento.Id,
                    orcamento.AprovadoEmUtc!.Value,
                    orcamento.VeiculoDescricaoSnapshot))
                .ToArrayAsync(cancellationToken));
        }

        return new(
            ordensEmExecucao,
            ordensAguardandoRetirada,
            orcamentosAguardandoAprovacao,
            servicosRealizados,
            veiculosEntregues,
            clientesAtendidos,
            orcamentosCriados,
            orcamentosEnviados,
            orcamentosAprovados,
            orcamentosRecusados,
            clientesQueChegaram,
            ordensEmExecucaoPeriodo,
            ordensAguardandoPeriodo,
            ordensConcluidasPeriodo,
            ranking,
            atividades.OrderByDescending(item => item.DataUtc).Take(limiteAtividades).ToArray());
    }

    private static async Task<IReadOnlyCollection<DashboardAtividadeItemDto>> ObterAtividadesOrdensAsync(
        IQueryable<OrdemServico> ordens,
        DashboardPeriodoDto periodo,
        int limite,
        CancellationToken cancellationToken)
    {
        var chegadas = await ordens
            .Where(ordem =>
                ordem.CheckInEmUtc >= periodo.InicioUtc &&
                ordem.CheckInEmUtc < periodo.FimExclusivoUtc)
            .OrderByDescending(ordem => ordem.CheckInEmUtc)
            .Take(limite)
            .Select(ordem => new
            {
                ordem.Id,
                DataUtc = ordem.CheckInEmUtc!.Value,
                Descricao = ordem.VeiculoDescricaoSnapshot
            })
            .ToArrayAsync(cancellationToken);
        var iniciadas = await ordens
            .Where(ordem =>
                ordem.IniciadaEmUtc >= periodo.InicioUtc &&
                ordem.IniciadaEmUtc < periodo.FimExclusivoUtc)
            .OrderByDescending(ordem => ordem.IniciadaEmUtc)
            .Take(limite)
            .Select(ordem => new
            {
                ordem.Id,
                DataUtc = ordem.IniciadaEmUtc!.Value,
                Descricao = ordem.VeiculoDescricaoSnapshot
            })
            .ToArrayAsync(cancellationToken);
        var entregues = await ordens
            .Where(ordem =>
                ordem.ConcluidaEmUtc >= periodo.InicioUtc &&
                ordem.ConcluidaEmUtc < periodo.FimExclusivoUtc)
            .OrderByDescending(ordem => ordem.ConcluidaEmUtc)
            .Take(limite)
            .Select(ordem => new
            {
                ordem.Id,
                DataUtc = ordem.ConcluidaEmUtc!.Value,
                Descricao = ordem.VeiculoDescricaoSnapshot
            })
            .ToArrayAsync(cancellationToken);

        return chegadas.Select(item => new DashboardAtividadeItemDto(
                TipoAtividadeDashboard.ClienteChegou, item.Id, item.DataUtc, item.Descricao))
            .Concat(iniciadas.Select(item => new DashboardAtividadeItemDto(
                TipoAtividadeDashboard.OrdemServicoIniciada, item.Id, item.DataUtc, item.Descricao)))
            .Concat(entregues.Select(item => new DashboardAtividadeItemDto(
                TipoAtividadeDashboard.VeiculoEntregue, item.Id, item.DataUtc, item.Descricao)))
            .OrderByDescending(item => item.DataUtc)
            .Take(limite)
            .ToArray();
    }
}
