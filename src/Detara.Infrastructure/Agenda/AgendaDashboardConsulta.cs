using Detara.Application.Dashboard;
using Detara.Domain.Agenda;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Agenda;

internal sealed class AgendaDashboardConsulta(DetaraDbContext db) : IAgendaDashboardConsulta
{
    public async Task<DashboardAgendaConsultaResultado> ObterAsync(
        Guid empresaId,
        DashboardPeriodoDto periodo,
        DateTime inicioHojeUtc,
        DateTime fimHojeExclusivoUtc,
        DateTime agoraUtc,
        int limiteAgenda,
        int limiteAtividades,
        CancellationToken cancellationToken)
    {
        var validos = db.Agendamentos
            .AsNoTracking()
            .Where(agendamento =>
                agendamento.EmpresaId == empresaId &&
                agendamento.Status != StatusAgendamento.Cancelado &&
                agendamento.Status != StatusAgendamento.NaoCompareceu);
        var hoje = validos.Where(agendamento =>
            agendamento.InicioUtc >= inicioHojeUtc &&
            agendamento.InicioUtc < fimHojeExclusivoUtc);

        var totaisHoje = await hoje
            .GroupBy(_ => 1)
            .Select(grupo => new
            {
                Total = grupo.Count(),
                Concluidos = grupo.Count(item => item.Status == StatusAgendamento.Concluido),
                Atrasados = grupo.Count(item =>
                    item.InicioUtc < agoraUtc &&
                    (item.Status == StatusAgendamento.Agendado ||
                     item.Status == StatusAgendamento.Confirmado))
            })
            .SingleOrDefaultAsync(cancellationToken);

        var itens = await hoje
            .OrderBy(agendamento => agendamento.InicioUtc)
            .Take(limiteAgenda)
            .Select(agendamento => new DashboardAgendamentoDto(
                agendamento.Id,
                agendamento.InicioUtc,
                agendamento.ClienteNomeSnapshot,
                agendamento.VeiculoDescricaoSnapshot,
                agendamento.VeiculoPlacaSnapshot,
                agendamento.Itens
                    .OrderBy(item => item.Ordem)
                    .Select(item => item.NomeSnapshot)
                    .FirstOrDefault(),
                agendamento.Status))
            .ToArrayAsync(cancellationToken);

        var agendamentosPeriodo = await validos.CountAsync(agendamento =>
            agendamento.InicioUtc >= periodo.InicioUtc &&
            agendamento.InicioUtc < periodo.FimExclusivoUtc,
            cancellationToken);

        var atividades = await db.Agendamentos
            .AsNoTracking()
            .Where(agendamento =>
                agendamento.EmpresaId == empresaId &&
                agendamento.CriadoEmUtc >= periodo.InicioUtc &&
                agendamento.CriadoEmUtc < periodo.FimExclusivoUtc)
            .OrderByDescending(agendamento => agendamento.CriadoEmUtc)
            .Take(limiteAtividades)
            .Select(agendamento => new DashboardAtividadeItemDto(
                TipoAtividadeDashboard.AgendamentoCriado,
                agendamento.Id,
                agendamento.CriadoEmUtc,
                agendamento.VeiculoDescricaoSnapshot))
            .ToArrayAsync(cancellationToken);

        return new(
            totaisHoje?.Total ?? 0,
            totaisHoje?.Concluidos ?? 0,
            agendamentosPeriodo,
            totaisHoje?.Atrasados ?? 0,
            itens,
            atividades);
    }
}
