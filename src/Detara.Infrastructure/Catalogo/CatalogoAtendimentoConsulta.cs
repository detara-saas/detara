using Detara.Application.Atendimento;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Catalogo;

internal sealed class CatalogoAtendimentoConsulta(DetaraDbContext db) : ICatalogoAtendimentoConsulta
{
    public async Task<IReadOnlyCollection<ItemCatalogoAtendimentoInterno>> ObterItensAsync(Guid empresaId,
        IReadOnlyCollection<(TipoItemOrcamento Tipo, Guid Id)> itens, CancellationToken ct)
    {
        var servicoIds = itens.Where(x => x.Tipo == TipoItemOrcamento.Servico).Select(x => x.Id).ToArray();
        var pacoteIds = itens.Where(x => x.Tipo == TipoItemOrcamento.Pacote).Select(x => x.Id).ToArray();
        var servicos = await db.Servicos.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && servicoIds.Contains(x.Id))
            .Select(x => new ItemCatalogoAtendimentoInterno(TipoItemOrcamento.Servico, x.Id, x.Nome, x.Descricao, x.TipoPrecificacao, x.PrecoBase, x.EhAtivo)).ToArrayAsync(ct);
        var pacotes = await db.Pacotes.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && pacoteIds.Contains(x.Id))
            .Select(x => new ItemCatalogoAtendimentoInterno(TipoItemOrcamento.Pacote, x.Id, x.Nome, x.Descricao, x.TipoPrecificacao, x.Preco, x.EhAtivo)).ToArrayAsync(ct);
        return servicos.Concat(pacotes).ToArray();
    }

    public async Task<IReadOnlyCollection<ItemCatalogoAtendimentoInterno>> BuscarItensAsync(Guid empresaId, string? pesquisa, int limite, CancellationToken ct)
    {
        var servicos = db.Servicos.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.EhAtivo);
        var pacotes = db.Pacotes.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.EhAtivo);
        if (!string.IsNullOrWhiteSpace(pesquisa)) { var termo = pesquisa.Trim(); servicos = servicos.Where(x => x.Nome.Contains(termo)); pacotes = pacotes.Where(x => x.Nome.Contains(termo)); }
        var a = await servicos.OrderBy(x => x.Nome).Take(limite).Select(x => new ItemCatalogoAtendimentoInterno(TipoItemOrcamento.Servico,
            x.Id, x.Nome, x.Descricao, x.TipoPrecificacao, x.PrecoBase, x.EhAtivo)).ToArrayAsync(ct);
        var b = await pacotes.OrderBy(x => x.Nome).Take(limite).Select(x => new ItemCatalogoAtendimentoInterno(TipoItemOrcamento.Pacote,
            x.Id, x.Nome, x.Descricao, x.TipoPrecificacao, x.Preco, x.EhAtivo)).ToArrayAsync(ct);
        return a.Concat(b).OrderBy(x => x.Nome).Take(limite).ToArray();
    }
}
