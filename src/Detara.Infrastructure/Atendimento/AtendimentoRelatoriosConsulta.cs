using Detara.Application.Relatorios;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Atendimento;

internal sealed class AtendimentoRelatoriosConsulta(DetaraDbContext db) : IAtendimentoRelatoriosConsulta
{
    public async Task<AtendimentoRelatorio> ObterAsync(Guid empresaId, IntervaloRelatorio p, string fuso,
        bool servicos, bool clientes, bool dias, CancellationToken ct)
    {
        var concluidas = db.OrdensServico.AsNoTracking().Where(x => x.EmpresaId == empresaId && x.Status == StatusOrdemServico.Concluida);
        var ordens = concluidas.Where(x => x.ConcluidaEmUtc >= p.InicioUtc && x.ConcluidaEmUtc < p.FimExclusivoUtc);
        // Soma correlacionada interna a Atendimento. Nenhum join com Financeiro multiplica itens ou valores.
        var totais = ordens.Select(x => new
        {
            x.ClienteId,
            x.ClienteNomeSnapshot,
            Valor = x.Itens.Sum(i => i.ValorUnitarioAutorizado * i.Quantidade) - x.DescontoAutorizado + x.AcrescimoAutorizado
        });
        var quantidade = await ordens.CountAsync(ct);
        var valor = await totais.SumAsync(x => (decimal?)x.Valor, ct) ?? 0;
        var clientesCount = await ordens.Select(x => x.ClienteId).Distinct().CountAsync(ct);
        var novos = await ordens.Where(x => !concluidas.Any(anterior => anterior.ClienteId == x.ClienteId && anterior.ConcluidaEmUtc < p.InicioUtc))
            .Select(x => x.ClienteId).Distinct().CountAsync(ct);
        var itens = db.OrdensServicoItens.AsNoTracking().Where(x => x.EmpresaId == empresaId &&
            x.OrdemServico.Status == StatusOrdemServico.Concluida && x.OrdemServico.ConcluidaEmUtc >= p.InicioUtc && x.OrdemServico.ConcluidaEmUtc < p.FimExclusivoUtc);
        var quantidadeServicos = await itens.SumAsync(x => (int?)x.Quantidade, ct) ?? 0;
        IReadOnlyList<RankingRelatorio> sq = [], sv = [], cc = [], cf = [];
        if (servicos)
        {
            // ID + tipo mantêm a identidade mesmo após renomear catálogo. Personalizados não têm identidade global:
            // cada linha permanece distinta para não fundir serviços diferentes com nomes coincidentes.
            var grupos = itens.GroupBy(x => new { x.TipoItem, Id = x.ItemCatalogoId ?? x.Id })
                .Select(g => new { g.Key.Id, Nome = g.Max(x => x.NomeSnapshot)!, Quantidade = g.Sum(x => x.Quantidade), Valor = g.Sum(x => x.ValorUnitarioAutorizado * x.Quantidade) });
            var porQuantidade = await grupos.OrderByDescending(x => x.Quantidade).ThenByDescending(x => x.Valor).ThenBy(x => x.Id).Take(10).ToArrayAsync(ct);
            var porValor = await grupos.OrderByDescending(x => x.Valor).ThenByDescending(x => x.Quantidade).ThenBy(x => x.Id).Take(10).ToArrayAsync(ct);
            sq = porQuantidade.Select(x => new RankingRelatorio(x.Id, x.Nome, x.Quantidade, x.Valor)).ToArray();
            sv = porValor.Select(x => new RankingRelatorio(x.Id, x.Nome, x.Quantidade, x.Valor)).ToArray();
        }
        if (clientes)
        {
            var grupos = totais.GroupBy(x => x.ClienteId).Select(g => new
            { Id = g.Key, Nome = g.Max(x => x.ClienteNomeSnapshot)!, Quantidade = g.Count(), Valor = g.Sum(x => x.Valor) });
            var consumo = await grupos.OrderByDescending(x => x.Valor).ThenByDescending(x => x.Quantidade).ThenBy(x => x.Id).Take(10).ToArrayAsync(ct);
            var frequencia = await grupos.OrderByDescending(x => x.Quantidade).ThenByDescending(x => x.Valor).ThenBy(x => x.Id).Take(10).ToArrayAsync(ct);
            cc = consumo.Select(x => new RankingRelatorio(x.Id, x.Nome, x.Quantidade, x.Valor)).ToArray();
            cf = frequencia.Select(x => new RankingRelatorio(x.Id, x.Nome, x.Quantidade, x.Valor)).ToArray();
        }
        var porDia = dias ? await SeriesRelatorio.AgregarAsync(ordens.Select(x => new MovimentoRelatorio { Data = x.ConcluidaEmUtc!.Value, Valor = 0, Quantidade = 1 }), db.Database.IsSqlServer(), fuso, ct) : [];
        return new(quantidade, valor, clientesCount, novos, quantidadeServicos, sq, sv, cc, cf, porDia);
    }

    public async Task<OrcamentosRelatorio> OrcamentosAsync(Guid empresaId, IntervaloRelatorio p, CancellationToken ct)
    {
        var query = db.Orcamentos.AsNoTracking().Where(x => x.EmpresaId == empresaId);
        var criados = await query.CountAsync(x => x.CriadoEmUtc >= p.InicioUtc && x.CriadoEmUtc < p.FimExclusivoUtc, ct);
        var aprovados = await query.CountAsync(x => x.Status == StatusOrcamento.Aprovado && x.AprovadoEmUtc >= p.InicioUtc && x.AprovadoEmUtc < p.FimExclusivoUtc, ct);
        var recusados = await query.CountAsync(x => x.Status == StatusOrcamento.Recusado && x.RecusadoEmUtc >= p.InicioUtc && x.RecusadoEmUtc < p.FimExclusivoUtc, ct);
        return new(criados, aprovados, recusados);
    }
}
