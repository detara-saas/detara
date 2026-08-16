using Detara.Application.Atendimento;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Agenda;

internal sealed class AgendaAtendimentoConsulta(DetaraDbContext db) : IAgendaAtendimentoConsulta
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
                Itens = x.Itens.OrderBy(i => i.Ordem).Select(i => new ItemAgendamentoAtendimentoInterno(
                    i.TipoItem == TipoItemAgendamento.Servico ? TipoItemOrcamento.Servico : TipoItemOrcamento.Pacote,
                    i.ItemCatalogoId, i.NomeSnapshot, i.DescricaoSnapshot, i.TipoPrecificacaoSnapshot, i.PrecoReferenciaSnapshot)).ToArray()
            })
            .SingleOrDefaultAsync(ct);
        return dado is null ? null : new(dado.Id, dado.ClienteId, dado.ClienteNome, dado.VeiculoId, dado.VeiculoDescricao, dado.VeiculoPlaca, dado.Itens);
    }
}
