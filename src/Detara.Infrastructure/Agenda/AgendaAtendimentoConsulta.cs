using Detara.Application.Atendimento;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Agenda;

internal sealed class AgendaAtendimentoIntegracao(DetaraDbContext db) : IAgendaAtendimentoIntegracao
{
    public async Task<AgendamentoAtendimentoInterno?> ObterAsync(Guid empresaId, Guid agendamentoId, CancellationToken ct)
    {
        var dado = await db.Agendamentos.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.Id == agendamentoId)
            .Select(x => new
            {
                x.Id,
                x.ClienteId,
                ClienteNome = x.ClienteNomeSnapshot,
                x.VeiculoId,
                VeiculoDescricao = x.VeiculoDescricaoSnapshot,
                VeiculoPlaca = x.VeiculoPlacaSnapshot,
                x.Status,
                x.DuracaoPlanejadaMinutos,
                Itens = x.Itens.OrderBy(i => i.Ordem).Select(i => new ItemAgendamentoAtendimentoInterno(
                    i.TipoItem == TipoItemAgendamento.Servico ? TipoItemOrcamento.Servico : TipoItemOrcamento.Pacote,
                    i.ItemCatalogoId, i.NomeSnapshot, i.DescricaoSnapshot, i.TipoPrecificacaoSnapshot, i.PrecoReferenciaSnapshot,
                    i.DuracaoReferenciaMinutosSnapshot)).ToArray()
            })
            .SingleOrDefaultAsync(ct);
        return dado is null ? null : new(dado.Id, dado.ClienteId, dado.ClienteNome, dado.VeiculoId, dado.VeiculoDescricao,
            dado.VeiculoPlaca, dado.Status, dado.Itens, dado.DuracaoPlanejadaMinutos);
    }

    public Task<AgendamentoAtendimentoInterno> AdicionarDeOrcamentoAsync(Guid empresaId,
        CriarAgendamentoOrcamentoInterno entrada, CancellationToken ct)
    {
        var itens = entrada.Itens.Where(item => item.ItemCatalogoId != Guid.Empty)
            .Select(item => new ItemAgendamentoSnapshot(
                item.TipoItem == TipoItemOrcamento.Servico ? TipoItemAgendamento.Servico : TipoItemAgendamento.Pacote,
                item.ItemCatalogoId, item.Nome, item.Descricao, item.TipoPrecificacao,
                item.PrecoReferencia, item.DuracaoReferenciaMinutos))
            .ToArray();
        var entidade = Agendamento.CriarDeOrcamento(empresaId, entrada.ClienteId, entrada.ClienteNome,
            entrada.VeiculoId, entrada.VeiculoDescricao, entrada.VeiculoPlaca, entrada.InicioUtc,
            entrada.DuracaoPlanejadaMinutos, entrada.ObservacaoSolicitante, entrada.ObservacaoInterna,
            itens);
        db.Agendamentos.Add(entidade);
        return Task.FromResult(new AgendamentoAtendimentoInterno(entidade.Id, entidade.ClienteId,
            entidade.ClienteNomeSnapshot, entidade.VeiculoId, entidade.VeiculoDescricaoSnapshot,
            entidade.VeiculoPlacaSnapshot, entidade.Status, entrada.Itens, entidade.DuracaoPlanejadaMinutos));
    }

    public async Task MarcarEmAtendimentoAsync(Guid empresaId, Guid agendamentoId, CancellationToken ct)
    {
        var entidade = await ObterParaAlteracaoAsync(empresaId, agendamentoId, ct);
        if (entidade.Status is StatusAgendamento.Agendado or StatusAgendamento.Confirmado)
            entidade.AlterarStatus(StatusAgendamento.Compareceu);
        else if (entidade.Status != StatusAgendamento.Compareceu)
            throw new InvalidOperationException("O agendamento não está disponível para iniciar o atendimento.");
    }

    public async Task ConcluirAsync(Guid empresaId, Guid agendamentoId, CancellationToken ct)
    {
        var entidade = await ObterParaAlteracaoAsync(empresaId, agendamentoId, ct);
        if (entidade.Status == StatusAgendamento.Compareceu)
            entidade.AlterarStatus(StatusAgendamento.Concluido);
        else if (entidade.Status != StatusAgendamento.Concluido)
            throw new InvalidOperationException("O agendamento não está em atendimento para ser concluído.");
    }

    private async Task<Agendamento> ObterParaAlteracaoAsync(Guid empresaId, Guid agendamentoId, CancellationToken ct) =>
        await db.Agendamentos.IgnoreQueryFilters().SingleOrDefaultAsync(
            item => item.EmpresaId == empresaId && item.Id == agendamentoId, ct)
        ?? throw new InvalidOperationException("Agendamento relacionado não encontrado.");
}
