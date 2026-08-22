using Detara.Application.Abstracoes;
using Detara.Application.Atendimento;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Atendimento;

internal sealed class OrdensServicoRepositorio(DetaraDbContext db) : IOrdensServicoRepositorio
{
    public async Task<PaginacaoResultado<OrdemServicoListaResultado>> ListarAsync(FiltroOrdensServico filtro, CancellationToken ct)
    {
        var query = db.OrdensServico.AsNoTracking();
        if (filtro.Status.HasValue) query = query.Where(item => item.Status == filtro.Status);
        if (filtro.DataInicial.HasValue)
        {
            var inicio = filtro.DataInicial.Value.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(item => item.CriadoEmUtc >= inicio);
        }
        if (filtro.DataFinal.HasValue)
        {
            var fimExclusivo = filtro.DataFinal.Value.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
            query = query.Where(item => item.CriadoEmUtc < fimExclusivo);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Pesquisa))
        {
            var termo = filtro.Pesquisa.Trim();
            var normalizado = new string(termo.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            query = query.Where(item => item.Codigo.Contains(termo) || item.ClienteNomeSnapshot.Contains(termo) ||
                item.VeiculoDescricaoSnapshot.Contains(termo) || item.VeiculoPlacaSnapshot.Contains(normalizado));
        }
        var total = await query.CountAsync(ct);
        var itens = await query.OrderByDescending(item => item.CriadoEmUtc)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina).Take(filtro.TamanhoPagina)
            .Select(item => new OrdemServicoListaResultado(item.Id, item.Codigo, item.ClienteNomeSnapshot,
                item.VeiculoDescricaoSnapshot, item.VeiculoPlacaSnapshot, item.Status,
                item.Itens.Sum(i => i.ValorUnitarioAutorizado * i.Quantidade) - item.DescontoAutorizado + item.AcrescimoAutorizado,
                item.CriadoEmUtc)).ToArrayAsync(ct);
        return new(itens, filtro.Pagina, filtro.TamanhoPagina, total);
    }

    public Task<OrdemServico?> ObterAsync(Guid id, bool paraAlteracao, CancellationToken ct)
    {
        var query = db.OrdensServico.Include(item => item.Itens).Include(item => item.Fotos)
            .Include(item => item.Historico).Include(item => item.Checklist).ThenInclude(item => item!.Itens)
            .Where(item => item.Id == id);
        return (paraAlteracao ? query : query.AsNoTracking()).SingleOrDefaultAsync(ct);
    }

    public Task<bool> ExistePorOrcamentoAsync(Guid orcamentoId, CancellationToken ct) =>
        db.OrdensServico.AnyAsync(item => item.OrcamentoOrigemId == orcamentoId, ct);
    public Task<bool> ExistePorAgendamentoAsync(Guid agendamentoId, CancellationToken ct) =>
        db.OrdensServico.AnyAsync(item => item.AgendamentoOrigemId == agendamentoId, ct);
    public Task<OrdemServicoAgendamentoResultado?> ObterPorAgendamentoAsync(Guid agendamentoId, CancellationToken ct) =>
        db.OrdensServico.AsNoTracking().Where(item => item.AgendamentoOrigemId == agendamentoId)
            .Select(item => new OrdemServicoAgendamentoResultado(item.Id, item.Codigo, item.Status))
            .SingleOrDefaultAsync(ct);

    public Task<OrdemServico?> ObterPorOrcamentoAdicionalAsync(Guid orcamentoId, CancellationToken ct) =>
        db.OrdensServico.Include(item => item.Itens).Include(item => item.Fotos).Include(item => item.Historico)
            .Include(item => item.Checklist).ThenInclude(item => item!.Itens)
            .SingleOrDefaultAsync(item => db.Orcamentos.Any(orcamento =>
                orcamento.Id == orcamentoId && orcamento.OrdemServicoOrigemId == item.Id), ct);

    public async Task<IReadOnlyCollection<Orcamento>> ListarOrcamentosAdicionaisAsync(Guid ordemServicoId, CancellationToken ct) =>
        await db.Orcamentos.AsNoTracking().Include(item => item.Itens)
            .Where(item => item.OrdemServicoOrigemId == ordemServicoId)
            .OrderByDescending(item => item.CriadoEmUtc).ToArrayAsync(ct);

    public void Adicionar(OrdemServico ordemServico) => db.OrdensServico.Add(ordemServico);
    public void AdicionarChecklist(OrdemServicoChecklist checklist) => db.OrdensServicoChecklists.Add(checklist);
    public void AdicionarItens(IReadOnlyCollection<OrdemServicoItem> itens) => db.OrdensServicoItens.AddRange(itens);
    public void AdicionarUltimoHistorico(OrdemServico ordemServico) => db.OrdensServicoHistoricosStatus.Add(ordemServico.Historico.Last());
    public void AdicionarFoto(OrdemServicoFoto foto) => db.OrdensServicoFotos.Add(foto);
    public void RemoverFoto(OrdemServicoFoto foto) => db.OrdensServicoFotos.Remove(foto);
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
