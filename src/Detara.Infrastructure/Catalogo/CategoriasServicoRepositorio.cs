using Detara.Application.Abstracoes;
using Detara.Application.Catalogo;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Catalogo;

internal sealed class CategoriasServicoRepositorio(DetaraDbContext db) : ICategoriasServicoRepositorio
{
    public async Task<IReadOnlyCollection<CategoriaServicoResultado>> ListarAsync(bool? ehAtivo, CancellationToken ct)
    {
        var query = db.CategoriasServico.AsNoTracking();
        if (ehAtivo.HasValue) query = query.Where(x => x.EhAtivo == ehAtivo.Value);
        return await query.OrderBy(x => x.Ordem).ThenBy(x => x.Nome).Select(x => new CategoriaServicoResultado(x.Id, x.Nome, x.Descricao, x.Ordem, x.Servicos.Count, x.EhAtivo)).ToArrayAsync(ct);
    }
    public Task<CategoriaServico?> ObterParaAlteracaoAsync(Guid id, CancellationToken ct) => db.CategoriasServico.Include(x => x.Servicos).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> NomeEmUsoAsync(string nome, Guid? ignorarId, CancellationToken ct) => db.CategoriasServico.AnyAsync(x => x.Nome == nome && (!ignorarId.HasValue || x.Id != ignorarId), ct);
    public Task<bool> PertenceAoTenantEAtivaAsync(Guid id, Guid empresaId, CancellationToken ct) => db.CategoriasServico.IgnoreQueryFilters().AnyAsync(x => x.Id == id && x.EmpresaId == empresaId && x.EhAtivo, ct);
    public void Adicionar(CategoriaServico categoria) => db.CategoriasServico.Add(categoria);
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
