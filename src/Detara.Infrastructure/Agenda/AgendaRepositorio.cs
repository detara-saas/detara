using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Domain.Agenda;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Agenda;

internal sealed class AgendaRepositorio(DetaraDbContext db) : IAgendaRepositorio
{
    public async Task<IReadOnlyCollection<AgendamentoPeriodoResultado>> ListarPeriodoAsync(FiltroAgendaPeriodo filtro, CancellationToken ct)
    {
        var query = AplicarFiltros(db.Agendamentos.AsNoTracking().Where(x => x.InicioUtc < filtro.FimUtc && x.InicioUtc.AddMinutes(x.DuracaoPlanejadaMinutos) > filtro.InicioUtc), filtro.Status, filtro.Pesquisa);
        var dados = await query.OrderBy(x => x.InicioUtc).Select(x => new
        {
            x.Id,
            x.InicioUtc,
            x.DuracaoPlanejadaMinutos,
            ClienteNome = x.ClienteNomeSnapshot,
            VeiculoDescricao = x.VeiculoDescricaoSnapshot,
            VeiculoPlaca = x.VeiculoPlacaSnapshot,
            x.Status,
            Itens = x.Itens.OrderBy(i => i.Ordem).Select(i => new { i.NomeSnapshot, i.TipoPrecificacaoSnapshot, i.PrecoReferenciaSnapshot }).ToArray()
        }).ToArrayAsync(ct);
        return dados.Select(x => new AgendamentoPeriodoResultado(x.Id, x.InicioUtc, x.DuracaoPlanejadaMinutos, x.ClienteNome, x.VeiculoDescricao, x.VeiculoPlaca, x.Status, x.Itens.Take(3).Select(i => i.NomeSnapshot).ToArray(), Resumir(x.Itens.Select(i => (i.TipoPrecificacaoSnapshot, i.PrecoReferenciaSnapshot))))).ToArray();
    }

    public async Task<PaginacaoResultado<AgendamentoListaResultado>> ListarHistoricoAsync(FiltroHistoricoAgendamentos filtro, CancellationToken ct)
    {
        var query = db.Agendamentos.AsNoTracking();
        if (filtro.InicioUtc.HasValue) query = query.Where(x => x.InicioUtc >= filtro.InicioUtc.Value);
        if (filtro.FimUtc.HasValue) query = query.Where(x => x.InicioUtc < filtro.FimUtc.Value);
        query = AplicarFiltros(query, filtro.Status, filtro.Pesquisa).OrderByDescending(x => x.InicioUtc);
        var total = await query.CountAsync(ct);
        var dados = await query.Skip((filtro.Pagina - 1) * filtro.TamanhoPagina).Take(filtro.TamanhoPagina).Select(x => new
        {
            x.Id,
            x.InicioUtc,
            x.DuracaoPlanejadaMinutos,
            ClienteNome = x.ClienteNomeSnapshot,
            VeiculoDescricao = x.VeiculoDescricaoSnapshot,
            VeiculoPlaca = x.VeiculoPlacaSnapshot,
            x.Status,
            Itens = x.Itens.OrderBy(i => i.Ordem).Select(i => i.NomeSnapshot).ToArray()
        }).ToArrayAsync(ct);
        return new(dados.Select(x => new AgendamentoListaResultado(x.Id, x.InicioUtc, x.DuracaoPlanejadaMinutos, x.ClienteNome, x.VeiculoDescricao, x.VeiculoPlaca, x.Status, x.Itens)).ToArray(), filtro.Pagina, filtro.TamanhoPagina, total);
    }

    public async Task<AgendamentoDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken ct)
    {
        var dado = await db.Agendamentos.AsNoTracking().Where(x => x.Id == id).Select(x => new
        {
            x.Id,
            x.ClienteId,
            ClienteNome = x.ClienteNomeSnapshot,
            x.VeiculoId,
            VeiculoDescricao = x.VeiculoDescricaoSnapshot,
            VeiculoPlaca = x.VeiculoPlacaSnapshot,
            x.InicioUtc,
            x.DuracaoPlanejadaMinutos,
            x.Status,
            x.ObservacaoSolicitante,
            x.ObservacaoInterna,
            x.MotivoCancelamento,
            x.CriadoEmUtc,
            x.AtualizadoEmUtc,
            Itens = x.Itens.OrderBy(i => i.Ordem).Select(i => new AgendamentoItemResultado(i.Id, i.TipoItem, i.ItemCatalogoId, i.NomeSnapshot, i.DescricaoSnapshot, i.TipoPrecificacaoSnapshot, i.PrecoReferenciaSnapshot, i.DuracaoReferenciaMinutosSnapshot, i.Ordem)).ToArray()
        }).SingleOrDefaultAsync(ct);
        return dado is null ? null : new(dado.Id, dado.ClienteId, dado.ClienteNome, dado.VeiculoId, dado.VeiculoDescricao, dado.VeiculoPlaca, dado.InicioUtc, dado.DuracaoPlanejadaMinutos, dado.Status, dado.ObservacaoSolicitante, dado.ObservacaoInterna, dado.MotivoCancelamento, dado.CriadoEmUtc, dado.AtualizadoEmUtc, dado.Itens, Resumir(dado.Itens.Select(i => (i.TipoPrecificacao, i.PrecoReferencia))));
    }

    public Task<Agendamento?> ObterParaAlteracaoAsync(Guid id, CancellationToken ct) => db.Agendamentos.Include(x => x.Itens).SingleOrDefaultAsync(x => x.Id == id, ct);
    public Task<int> ContarSobreposicoesAsync(DateTime inicioUtc, DateTime fimUtc, Guid? ignorarAgendamentoId, CancellationToken ct) => db.Agendamentos.AsNoTracking().CountAsync(x => (!ignorarAgendamentoId.HasValue || x.Id != ignorarAgendamentoId.Value) && x.Status != StatusAgendamento.Cancelado && x.Status != StatusAgendamento.NaoCompareceu && x.Status != StatusAgendamento.Concluido && x.InicioUtc < fimUtc && x.InicioUtc.AddMinutes(x.DuracaoPlanejadaMinutos) > inicioUtc, ct);
    public void Adicionar(Agendamento agendamento) => db.Agendamentos.Add(agendamento);
    public void RemoverItensAtuais(Agendamento agendamento) => db.AgendamentosItens.RemoveRange(agendamento.Itens);
    public void AdicionarItensAtuais(Agendamento agendamento) => db.AgendamentosItens.AddRange(agendamento.Itens);
    public Task SalvarAsync(CancellationToken ct) => db.SaveChangesAsync(ct);

    private static IQueryable<Agendamento> AplicarFiltros(IQueryable<Agendamento> query, StatusAgendamento? status, string? pesquisa)
    {
        if (status.HasValue) query = query.Where(x => x.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(pesquisa)) { var termo = pesquisa.Trim(); var placa = new string(termo.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray()); query = query.Where(x => x.ClienteNomeSnapshot.Contains(termo) || x.VeiculoDescricaoSnapshot.Contains(termo) || x.VeiculoPlacaSnapshot != null && x.VeiculoPlacaSnapshot.Contains(placa) || x.Itens.Any(i => i.NomeSnapshot.Contains(termo))); }
        return query;
    }

    private static ResumoReferenciaAgenda Resumir(IEnumerable<(Domain.Catalogo.TipoPrecificacao Tipo, decimal? Preco)> itens)
    {
        var dados = itens.ToArray();
        var sobConsulta = dados.Any(x => x.Tipo == Domain.Catalogo.TipoPrecificacao.SobConsulta);
        var aPartirDe = dados.Any(x => x.Tipo == Domain.Catalogo.TipoPrecificacao.APartirDe);
        decimal? soma = sobConsulta || dados.Any(x => !x.Preco.HasValue) ? null : dados.Sum(x => x.Preco!.Value);
        return new(soma, aPartirDe, sobConsulta);
    }
}
