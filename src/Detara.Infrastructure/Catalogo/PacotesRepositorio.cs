using Detara.Application.Abstracoes;
using Detara.Application.Catalogo;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Catalogo;

internal sealed class PacotesRepositorio(DetaraDbContext db) : IPacotesRepositorio
{
    public async Task<PaginacaoResultado<PacoteListaItemResultado>> ListarAsync(FiltroPacotes filtro, CancellationToken ct)
    {
        var query = db.Pacotes.AsNoTracking();
        if (filtro.EhAtivo.HasValue) query = query.Where(x => x.EhAtivo == filtro.EhAtivo);
        if (!string.IsNullOrWhiteSpace(filtro.Pesquisa)) { var pesquisa = filtro.Pesquisa.Trim(); query = query.Where(x => x.Nome.Contains(pesquisa) || x.Servicos.Any(s => s.Servico.Nome.Contains(pesquisa))); }
        query = query.OrderBy(x => x.Nome);
        var total = await query.CountAsync(ct);
        var itens = await query.Skip((filtro.Pagina - 1) * filtro.TamanhoPagina).Take(filtro.TamanhoPagina).Select(x => new PacoteListaItemResultado(
            x.Id, x.Nome, x.Servicos.Count, x.TipoPrecificacao, x.Preco,
            x.Servicos.Any(s => s.Servico.PrecoBase == null) ? null : x.Servicos.Sum(s => s.Servico.PrecoBase),
            x.Preco.HasValue && !x.Servicos.Any(s => s.Servico.PrecoBase == null) && x.Servicos.Sum(s => s.Servico.PrecoBase) > x.Preco ? x.Servicos.Sum(s => s.Servico.PrecoBase) - x.Preco : null,
            x.Servicos.Any(s => s.Servico.DuracaoEstimadaMinutos == null) ? null : x.Servicos.Sum(s => s.Servico.DuracaoEstimadaMinutos), x.EhAtivo)).ToArrayAsync(ct);
        return new(itens, filtro.Pagina, filtro.TamanhoPagina, total);
    }
    public Task<PacoteDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken ct) => db.Pacotes.AsNoTracking().Where(x => x.Id == id).Select(x => new PacoteDetalheResultado(
        x.Id, x.Nome, x.Descricao, x.TipoPrecificacao, x.Preco,
        x.Servicos.Any(s => s.Servico.PrecoBase == null) ? null : x.Servicos.Sum(s => s.Servico.PrecoBase),
        x.Preco.HasValue && !x.Servicos.Any(s => s.Servico.PrecoBase == null) && x.Servicos.Sum(s => s.Servico.PrecoBase) > x.Preco ? x.Servicos.Sum(s => s.Servico.PrecoBase) - x.Preco : null,
        x.Servicos.Any(s => s.Servico.DuracaoEstimadaMinutos == null) ? null : x.Servicos.Sum(s => s.Servico.DuracaoEstimadaMinutos),
        x.CriadoEmUtc, x.AtualizadoEmUtc, x.EhAtivo,
        x.Servicos.OrderBy(s => s.Ordem).Select(s => new PacoteServicoResultado(s.ServicoId, s.Servico.Nome, s.Servico.CategoriaServico.Nome, s.Servico.TipoPrecificacao, s.Servico.PrecoBase, s.Servico.DuracaoEstimadaMinutos, s.Ordem, s.Servico.EhAtivo)).ToArray())).SingleOrDefaultAsync(ct);
    public Task<Pacote?> ObterParaAlteracaoAsync(Guid id, CancellationToken ct) => db.Pacotes.Include(x => x.Servicos).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<bool> NomeEmUsoAsync(string nome, Guid? ignorarId, CancellationToken ct) => db.Pacotes.AnyAsync(x => x.Nome == nome && (!ignorarId.HasValue || x.Id != ignorarId), ct);
    public void Adicionar(Pacote pacote) => db.Pacotes.Add(pacote);
    public void RemoverComposicaoAtual(Pacote pacote) => db.PacotesServicos.RemoveRange(pacote.Servicos);
    public void AdicionarComposicaoAtual(Pacote pacote) => db.PacotesServicos.AddRange(pacote.Servicos);
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
