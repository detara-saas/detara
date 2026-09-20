using Detara.Application.Clientes;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Atendimento;

internal sealed class AtendimentoClienteRelacionamentoConsulta(DetaraDbContext db)
    : IAtendimentoClienteRelacionamentoConsulta
{
    public async Task<AtendimentoClienteRelacionamentoResultado> ObterAsync(
        Guid clienteId,
        bool incluirAtendimentos,
        bool incluirOrcamentos,
        CancellationToken cancellationToken)
    {
        var resumo = new ResumoRelacionamentoClienteResultado(0, 0, null, null, null, null);
        IReadOnlyCollection<VeiculoRelacionamentoAtendimentoResultado> veiculos = [];
        IReadOnlyCollection<AtendimentoRelacionamentoClienteResultado> atendimentos = [];
        IReadOnlyCollection<OrcamentoRelacionamentoClienteResultado> orcamentos = [];

        if (incluirAtendimentos)
        {
            var concluidas = db.OrdensServico.AsNoTracking()
                .Where(item => item.ClienteId == clienteId &&
                    item.Status == StatusOrdemServico.Concluida);
            var concluidasComVeiculo = concluidas.Where(item => item.VeiculoId.HasValue);
            var agrupadas = await concluidasComVeiculo
                .GroupBy(item => item.VeiculoId)
                .Select(grupo => new
                {
                    VeiculoId = grupo.Key!.Value,
                    QuantidadeAtendimentos = grupo.Count(),
                    QuantidadeServicos = grupo.Sum(ordem => ordem.Itens.Sum(item => item.Quantidade)),
                    TotalInvestido = grupo.Sum(ordem =>
                        ordem.Itens.Sum(item => item.ValorUnitarioAutorizado * item.Quantidade) -
                        ordem.DescontoAutorizado + ordem.AcrescimoAutorizado),
                    PrimeiraVisitaEmUtc = grupo.Min(ordem => ordem.ConcluidaEmUtc),
                    UltimaVisitaEmUtc = grupo.Max(ordem => ordem.ConcluidaEmUtc)
                })
                .ToArrayAsync(cancellationToken);
            var ultimosServicos = await concluidasComVeiculo
                .Where(ordem => ordem.ConcluidaEmUtc == db.OrdensServico
                    .Where(item => item.ClienteId == clienteId &&
                        item.VeiculoId == ordem.VeiculoId &&
                        item.Status == StatusOrdemServico.Concluida)
                    .Max(item => item.ConcluidaEmUtc))
                .Select(ordem => new
                {
                    VeiculoId = ordem.VeiculoId!.Value,
                    ordem.ConcluidaEmUtc,
                    Servico = ordem.Itens.OrderBy(item => item.Ordem)
                        .Select(item => item.NomeSnapshot)
                        .FirstOrDefault()
                })
                .ToArrayAsync(cancellationToken);
            var ultimoServicoPorVeiculo = ultimosServicos
                .GroupBy(item => item.VeiculoId)
                .ToDictionary(
                    grupo => grupo.Key,
                    grupo => grupo.OrderByDescending(item => item.ConcluidaEmUtc)
                        .Select(item => item.Servico)
                        .FirstOrDefault());
            veiculos = agrupadas.Select(item => new VeiculoRelacionamentoAtendimentoResultado(
                item.VeiculoId,
                item.QuantidadeAtendimentos,
                item.QuantidadeServicos,
                ultimoServicoPorVeiculo.GetValueOrDefault(item.VeiculoId),
                item.UltimaVisitaEmUtc)).ToArray();

            var totais = await concluidas.GroupBy(_ => 1).Select(grupo => new
            {
                QuantidadeAtendimentos = grupo.Count(),
                TotalInvestido = grupo.Sum(ordem =>
                    ordem.Itens.Sum(item => item.ValorUnitarioAutorizado * item.Quantidade) -
                    ordem.DescontoAutorizado + ordem.AcrescimoAutorizado),
                PrimeiraVisitaEmUtc = grupo.Min(ordem => ordem.ConcluidaEmUtc),
                UltimaVisitaEmUtc = grupo.Max(ordem => ordem.ConcluidaEmUtc)
            }).SingleOrDefaultAsync(cancellationToken);
            var quantidadeAtendimentos = totais?.QuantidadeAtendimentos ?? 0;
            var totalInvestido = totais?.TotalInvestido ?? 0;
            var primeiraVisita = totais?.PrimeiraVisitaEmUtc;
            var ultimaVisita = totais?.UltimaVisitaEmUtc;
            var servicoMaisRealizado = await db.OrdensServicoItens.AsNoTracking()
                .Where(item => item.OrdemServico.ClienteId == clienteId &&
                    item.OrdemServico.Status == StatusOrdemServico.Concluida)
                .GroupBy(item => item.NomeSnapshot)
                .Select(grupo => new { Nome = grupo.Key, Quantidade = grupo.Sum(item => item.Quantidade) })
                .OrderByDescending(item => item.Quantidade)
                .ThenBy(item => item.Nome)
                .Select(item => item.Nome)
                .FirstOrDefaultAsync(cancellationToken);
            var frequenciaRetornoDias = quantidadeAtendimentos >= 2 &&
                primeiraVisita.HasValue && ultimaVisita.HasValue
                ? (int?)Math.Round(
                    (ultimaVisita.Value - primeiraVisita.Value).TotalDays /
                    (quantidadeAtendimentos - 1),
                    MidpointRounding.AwayFromZero)
                : null;
            resumo = new ResumoRelacionamentoClienteResultado(
                quantidadeAtendimentos,
                totalInvestido,
                quantidadeAtendimentos > 0 ? totalInvestido / quantidadeAtendimentos : null,
                ultimaVisita,
                servicoMaisRealizado,
                frequenciaRetornoDias);

            atendimentos = await db.OrdensServico.AsNoTracking()
                .Where(item => item.ClienteId == clienteId)
                .OrderByDescending(item => item.ConcluidaEmUtc ?? item.ExecucaoFinalizadaEmUtc ??
                    item.IniciadaEmUtc ?? item.CheckInEmUtc ?? item.CriadoEmUtc)
                .Take(12)
                .Select(item => new AtendimentoRelacionamentoClienteResultado(
                    item.Id,
                    item.Codigo,
                    item.VeiculoId,
                    item.VeiculoDescricaoSnapshot,
                    item.VeiculoPlacaSnapshot,
                    item.Status,
                    item.Itens.Sum(servico => servico.ValorUnitarioAutorizado * servico.Quantidade) -
                        item.DescontoAutorizado + item.AcrescimoAutorizado,
                    item.ConcluidaEmUtc ?? item.ExecucaoFinalizadaEmUtc ?? item.IniciadaEmUtc ??
                        item.CheckInEmUtc ?? item.CriadoEmUtc,
                    item.Itens.OrderBy(servico => servico.Ordem)
                        .Select(servico => servico.NomeSnapshot)
                        .ToArray()))
                .ToArrayAsync(cancellationToken);
        }

        if (incluirOrcamentos)
        {
            var hoje = DateOnly.FromDateTime(DateTime.UtcNow);
            var itens = await db.Orcamentos.AsNoTracking()
                .Where(item => item.ClienteId == clienteId)
                .OrderByDescending(item => item.EmitidoEmUtc ?? item.CriadoEmUtc)
                .Take(8)
                .Select(item => new
                {
                    item.Id,
                    item.Codigo,
                    item.VeiculoId,
                    VeiculoDescricao = item.VeiculoDescricaoSnapshot,
                    VeiculoPlaca = item.VeiculoPlacaSnapshot,
                    item.Status,
                    item.ValidoAte,
                    Total = item.Itens.Sum(valor => valor.ValorUnitario * valor.Quantidade) -
                        item.Desconto + item.Acrescimo,
                    DataEmUtc = item.EmitidoEmUtc ?? item.CriadoEmUtc,
                    Itens = item.Itens.OrderBy(valor => valor.Ordem)
                        .Select(valor => valor.NomeSnapshot)
                        .ToArray()
                })
                .ToArrayAsync(cancellationToken);
            orcamentos = itens.Select(item => new OrcamentoRelacionamentoClienteResultado(
                item.Id,
                item.Codigo,
                item.VeiculoId,
                item.VeiculoDescricao,
                item.VeiculoPlaca,
                item.Status == StatusOrcamento.Emitido && item.ValidoAte < hoje
                    ? StatusEfetivoOrcamento.Expirado
                    : (StatusEfetivoOrcamento)(int)item.Status,
                item.Total,
                item.DataEmUtc,
                item.Itens)).ToArray();
        }

        return new AtendimentoClienteRelacionamentoResultado(
            resumo,
            veiculos,
            atendimentos,
            orcamentos);
    }
}
