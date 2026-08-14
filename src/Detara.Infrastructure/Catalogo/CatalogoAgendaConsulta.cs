using Detara.Application.Agenda;
using Detara.Domain.Agenda;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Catalogo;

internal sealed class CatalogoAgendaConsulta(DetaraDbContext db) : ICatalogoAgendaConsulta
{
    public async Task<IReadOnlyCollection<ItemCatalogoAgendaInterno>> ObterItensAsync(Guid empresaId, IReadOnlyCollection<(TipoItemAgendamento Tipo, Guid Id)> itens, CancellationToken ct)
    {
        var servicoIds = itens.Where(x => x.Tipo == TipoItemAgendamento.Servico).Select(x => x.Id).Distinct().ToArray();
        var pacoteIds = itens.Where(x => x.Tipo == TipoItemAgendamento.Pacote).Select(x => x.Id).Distinct().ToArray();
        var servicos = await ProjetarServicos(empresaId, servicoIds).ToArrayAsync(ct);
        var pacotes = await ProjetarPacotes(empresaId, pacoteIds).ToArrayAsync(ct);
        return servicos.Concat(pacotes).ToArray();
    }

    public async Task<IReadOnlyCollection<ItemCatalogoAgendaInterno>> BuscarItensAsync(Guid empresaId, string? pesquisa, bool incluirInativos, int limite, CancellationToken ct)
    {
        var servicos = db.Servicos.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && (incluirInativos || x.EhAtivo));
        var pacotes = db.Pacotes.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.EmpresaId == empresaId && (incluirInativos || x.EhAtivo));
        if (!string.IsNullOrWhiteSpace(pesquisa))
        {
            var termo = pesquisa.Trim();
            servicos = servicos.Where(x => x.Nome.Contains(termo) || x.CategoriaServico.Nome.Contains(termo));
            pacotes = pacotes.Where(x => x.Nome.Contains(termo));
        }

        var servicosProjetados = await servicos.OrderBy(x => x.Nome).Take(limite)
            .Select(x => new ItemCatalogoAgendaInterno(TipoItemAgendamento.Servico, x.Id, x.Nome, x.Descricao, x.CategoriaServico.Nome, x.TipoPrecificacao, x.PrecoBase, x.DuracaoEstimadaMinutos, x.EhAtivo))
            .ToArrayAsync(ct);
        var pacotesProjetados = await pacotes.OrderBy(x => x.Nome).Take(limite)
            .Select(x => new ItemCatalogoAgendaInterno(TipoItemAgendamento.Pacote, x.Id, x.Nome, x.Descricao, null, x.TipoPrecificacao, x.Preco, x.Servicos.Any(s => s.Servico.DuracaoEstimadaMinutos == null) ? null : x.Servicos.Sum(s => s.Servico.DuracaoEstimadaMinutos), x.EhAtivo))
            .ToArrayAsync(ct);
        var itens = servicosProjetados.Concat(pacotesProjetados);
        return itens.OrderBy(x => x.Nome).Take(limite).ToArray();
    }

    private IQueryable<ItemCatalogoAgendaInterno> ProjetarServicos(Guid empresaId, IReadOnlyCollection<Guid>? ids = null)
    {
        var query = db.Servicos.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId);
        if (ids is not null) query = query.Where(x => ids.Contains(x.Id));
        return query.Select(x => new ItemCatalogoAgendaInterno(TipoItemAgendamento.Servico, x.Id, x.Nome, x.Descricao, x.CategoriaServico.Nome, x.TipoPrecificacao, x.PrecoBase, x.DuracaoEstimadaMinutos, x.EhAtivo));
    }
    private IQueryable<ItemCatalogoAgendaInterno> ProjetarPacotes(Guid empresaId, IReadOnlyCollection<Guid>? ids = null)
    {
        var query = db.Pacotes.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId);
        if (ids is not null) query = query.Where(x => ids.Contains(x.Id));
        return query.Select(x => new ItemCatalogoAgendaInterno(TipoItemAgendamento.Pacote, x.Id, x.Nome, x.Descricao, null, x.TipoPrecificacao, x.Preco, x.Servicos.Any(s => s.Servico.DuracaoEstimadaMinutos == null) ? null : x.Servicos.Sum(s => s.Servico.DuracaoEstimadaMinutos), x.EhAtivo));
    }
}
