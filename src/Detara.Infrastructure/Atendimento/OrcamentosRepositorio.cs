using Detara.Application.Abstracoes;
using Detara.Application.Atendimento;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Atendimento;

internal sealed class OrcamentosRepositorio(DetaraDbContext db) : IOrcamentosRepositorio
{
    public async Task<PaginacaoResultado<OrcamentoListaResultado>> ListarAsync(FiltroOrcamentos filtro, CancellationToken ct)
    {
        var query = db.Orcamentos.AsNoTracking();
        if (filtro.Status.HasValue)
        {
            if (filtro.Status.Value == StatusEfetivoOrcamento.Expirado)
            {
                query = query.Where(x => x.Status == StatusOrcamento.Emitido && x.ValidoAte < filtro.HojeLocal);
            }
            else if (filtro.Status.Value == StatusEfetivoOrcamento.Emitido)
            {
                query = query.Where(x => x.Status == StatusOrcamento.Emitido && x.ValidoAte >= filtro.HojeLocal);
            }
            else
            {
                var statusPersistido = (StatusOrcamento)(int)filtro.Status.Value;
                query = query.Where(x => x.Status == statusPersistido);
            }
        }
        if (!string.IsNullOrWhiteSpace(filtro.Pesquisa))
        {
            var termo = filtro.Pesquisa.Trim();
            var normalizado = new string(termo.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            query = query.Where(x => x.Codigo != null && x.Codigo.Contains(termo) || x.ClienteNomeSnapshot.Contains(termo)
                || x.ClienteDocumentoSnapshot != null && x.ClienteDocumentoSnapshot.Contains(normalizado)
                || x.VeiculoDescricaoSnapshot.Contains(termo)
                || x.VeiculoPlacaSnapshot != null && x.VeiculoPlacaSnapshot.Contains(normalizado));
        }
        var total = await query.CountAsync(ct);
        var itens = await query.OrderByDescending(x => x.CriadoEmUtc)
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina).Take(filtro.TamanhoPagina)
            .Select(x => new OrcamentoListaResultado(x.Id, x.Codigo, x.ClienteNomeSnapshot, x.VeiculoDescricaoSnapshot,
                x.VeiculoPlacaSnapshot, x.EmitidoEmUtc, x.ValidoAte,
                x.Itens.Sum(i => i.ValorUnitario * i.Quantidade) - x.Desconto + x.Acrescimo, x.Status))
            .ToArrayAsync(ct);
        return new(itens, filtro.Pagina, filtro.TamanhoPagina, total);
    }

    public async Task<OrcamentoDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken ct)
    {
        var dado = await db.Orcamentos.AsNoTracking().Where(x => x.Id == id).Select(x => new
        {
            x.Id,
            x.Codigo,
            x.ClienteId,
            ClienteNome = x.ClienteNomeSnapshot,
            ClienteDocumento = x.ClienteDocumentoSnapshot,
            ClienteTelefone = x.ClienteTelefoneSnapshot,
            x.VeiculoId,
            VeiculoDescricao = x.VeiculoDescricaoSnapshot,
            VeiculoPlaca = x.VeiculoPlacaSnapshot,
            x.AgendamentoOrigemId,
            x.AgendamentoId,
            x.OrcamentoOrigemId,
            x.OrdemServicoOrigemId,
            x.Status,
            x.ValidoAte,
            x.ObservacaoCliente,
            x.ObservacaoInterna,
            x.Condicoes,
            x.Desconto,
            x.Acrescimo,
            x.CriadoEmUtc,
            x.AtualizadoEmUtc,
            x.EmitidoEmUtc,
            x.AprovadoEmUtc,
            x.RecusadoEmUtc,
            x.CanceladoEmUtc,
            x.SubstituidoEmUtc,
            x.AprovadoPorUsuarioId,
            Itens = x.Itens.OrderBy(i => i.Ordem).Select(i => new OrcamentoItemResultado(i.Id, i.TipoItem, i.ItemCatalogoId,
                i.NomeSnapshot, i.DescricaoSnapshot, i.TipoPrecificacaoReferenciaSnapshot, i.PrecoReferenciaSnapshot,
                i.ValorUnitario, i.Quantidade, i.Ordem, i.Observacao)).ToArray(),
            Historico = x.Historico.OrderBy(h => h.DataUtc).Select(h => new HistoricoStatusOrcamentoResultado(h.Id, h.Status,
                h.DataUtc, h.UsuarioId, h.Observacao)).ToArray()
        }).SingleOrDefaultAsync(ct);
        if (dado is null) return null;
        var ordemServicoId = await db.OrdensServico.AsNoTracking().Where(x => x.OrcamentoOrigemId == dado.Id)
            .Select(x => (Guid?)x.Id).SingleOrDefaultAsync(ct);
        var origem = dado.OrcamentoOrigemId.HasValue ? await ObterReferenciaAsync(dado.OrcamentoOrigemId.Value, ct) : null;
        var substituto = await db.Orcamentos.AsNoTracking().Where(x => x.OrcamentoOrigemId == dado.Id && x.Status != StatusOrcamento.Rascunho)
            .OrderByDescending(x => x.EmitidoEmUtc).Select(x => new ReferenciaOrcamentoResultado(x.Id, x.Codigo, x.Status, x.ValidoAte)).FirstOrDefaultAsync(ct);
        return new(dado.Id, dado.Codigo, dado.ClienteId, dado.ClienteNome, dado.ClienteDocumento, dado.ClienteTelefone,
            dado.VeiculoId, dado.VeiculoDescricao, dado.VeiculoPlaca, dado.AgendamentoOrigemId, dado.AgendamentoId, dado.OrcamentoOrigemId,
            dado.OrdemServicoOrigemId, ordemServicoId, dado.Status, dado.ValidoAte, dado.ObservacaoCliente, dado.ObservacaoInterna, dado.Condicoes, dado.Desconto,
            dado.Acrescimo, dado.CriadoEmUtc, dado.AtualizadoEmUtc, dado.EmitidoEmUtc, dado.AprovadoEmUtc, dado.RecusadoEmUtc,
            dado.CanceladoEmUtc, dado.SubstituidoEmUtc, dado.AprovadoPorUsuarioId, dado.Itens, dado.Historico, origem, substituto);
    }

    public Task<Orcamento?> ObterParaAlteracaoAsync(Guid id, CancellationToken ct) => db.Orcamentos.Include(x => x.Itens)
        .Include(x => x.Historico).SingleOrDefaultAsync(x => x.Id == id, ct);
    public async Task<IReadOnlyCollection<ReferenciaOrcamentoResultado>> ListarPorAgendamentoAsync(Guid agendamentoId, CancellationToken ct) =>
        await db.Orcamentos.AsNoTracking().Where(x => x.AgendamentoId == agendamentoId && !x.OrdemServicoOrigemId.HasValue)
            .OrderByDescending(x => x.CriadoEmUtc)
            .Select(x => new ReferenciaOrcamentoResultado(x.Id, x.Codigo, x.Status, x.ValidoAte))
            .ToArrayAsync(ct);
    public void Adicionar(Orcamento orcamento) => db.Orcamentos.Add(orcamento);
    public void RemoverItensAtuais(Orcamento orcamento) => db.OrcamentosItens.RemoveRange(orcamento.Itens);
    public void AdicionarItensAtuais(Orcamento orcamento) => db.OrcamentosItens.AddRange(orcamento.Itens);
    public void AdicionarUltimoHistorico(Orcamento orcamento) => db.OrcamentosHistoricosStatus.Add(orcamento.Historico.Last());
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private Task<ReferenciaOrcamentoResultado?> ObterReferenciaAsync(Guid id, CancellationToken ct) => db.Orcamentos.AsNoTracking()
        .Where(x => x.Id == id).Select(x => new ReferenciaOrcamentoResultado(x.Id, x.Codigo, x.Status, x.ValidoAte)).SingleOrDefaultAsync(ct);
}
