using Detara.Application.Abstracoes;
using Detara.Application.Catalogo;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Catalogo;

internal sealed class ServicosRepositorio(DetaraDbContext db) : IServicosRepositorio
{
    public async Task<PaginacaoResultado<ServicoListaItemResultado>> ListarAsync(FiltroServicos filtro, CancellationToken ct)
    {
        var query = db.Servicos.AsNoTracking();
        if (filtro.EhAtivo.HasValue) query = query.Where(x => x.EhAtivo == filtro.EhAtivo);
        if (filtro.CategoriaServicoId.HasValue) query = query.Where(x => x.CategoriaServicoId == filtro.CategoriaServicoId);
        if (!string.IsNullOrWhiteSpace(filtro.Pesquisa)) { var pesquisa = filtro.Pesquisa.Trim(); query = query.Where(x => x.Nome.Contains(pesquisa) || x.CategoriaServico.Nome.Contains(pesquisa)); }
        query = query.OrderBy(x => x.CategoriaServico.Ordem).ThenBy(x => x.CategoriaServico.Nome).ThenBy(x => x.Ordem).ThenBy(x => x.Nome);
        var total = await query.CountAsync(ct);
        var itens = await query.Skip((filtro.Pagina - 1) * filtro.TamanhoPagina).Take(filtro.TamanhoPagina)
            .Select(x => new ServicoListaItemResultado(x.Id, x.Nome, x.CategoriaServicoId, x.CategoriaServico.Nome, x.TipoPrecificacao, x.PrecoBase, x.DuracaoEstimadaMinutos, x.EhAtivo)).ToArrayAsync(ct);
        return new(itens, filtro.Pagina, filtro.TamanhoPagina, total);
    }
    public Task<ServicoDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken ct) => db.Servicos.AsNoTracking().Where(x => x.Id == id)
        .Select(x => new ServicoDetalheResultado(x.Id, x.CategoriaServicoId, x.CategoriaServico.Nome, x.Nome, x.Descricao, x.TipoPrecificacao, x.PrecoBase, x.DuracaoEstimadaMinutos, x.Ordem, x.CriadoEmUtc, x.AtualizadoEmUtc, x.EhAtivo)).SingleOrDefaultAsync(ct);
    public async Task<IReadOnlyCollection<ServicoSelecaoResultado>> ListarParaSelecaoAsync(bool incluirInativos, CancellationToken ct) =>
        await db.Servicos.AsNoTracking().Where(x => incluirInativos || x.EhAtivo).OrderBy(x => x.CategoriaServico.Ordem).ThenBy(x => x.CategoriaServico.Nome).ThenBy(x => x.Ordem).ThenBy(x => x.Nome)
            .Select(x => new ServicoSelecaoResultado(x.Id, x.Nome, x.CategoriaServico.Nome, x.TipoPrecificacao, x.PrecoBase, x.DuracaoEstimadaMinutos, x.EhAtivo)).ToArrayAsync(ct);
    public Task<Servico?> ObterParaAlteracaoAsync(Guid id, CancellationToken ct) => db.Servicos.SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> NomeEmUsoAsync(Guid categoriaId, string nome, Guid? ignorarId, CancellationToken ct) => db.Servicos.AnyAsync(x => x.CategoriaServicoId == categoriaId && x.Nome == nome && (!ignorarId.HasValue || x.Id != ignorarId), ct);
    public async Task<IReadOnlyCollection<Guid>> ObterIdsDoTenantAsync(IReadOnlyCollection<Guid> ids, Guid empresaId, CancellationToken ct) => await db.Servicos.IgnoreQueryFilters().Where(x => ids.Contains(x.Id) && x.EmpresaId == empresaId).Select(x => x.Id).ToArrayAsync(ct);
    public void Adicionar(Servico servico) => db.Servicos.Add(servico);
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
