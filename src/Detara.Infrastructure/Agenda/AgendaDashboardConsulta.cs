using Detara.Application.Dashboard;
using Detara.Domain.Agenda;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Agenda;

internal sealed class AgendaDashboardConsulta(DetaraDbContext db) : IAgendaDashboardConsulta
{
    public async Task<DashboardAgendaResultado> ObterAsync(
        Guid empresaId,
        DateTime inicioUtc,
        DateTime fimExclusivoUtc,
        int limite,
        CancellationToken cancellationToken)
    {
        var validos = db.Agendamentos
            .AsNoTracking()
            .Where(agendamento =>
                agendamento.EmpresaId == empresaId &&
                agendamento.InicioUtc >= inicioUtc &&
                agendamento.InicioUtc < fimExclusivoUtc &&
                agendamento.Status != StatusAgendamento.Cancelado &&
                agendamento.Status != StatusAgendamento.NaoCompareceu);

        var totais = await validos
            .GroupBy(_ => 1)
            .Select(grupo => new
            {
                Total = grupo.Count(),
                Concluidos = grupo.Count(item => item.Status == StatusAgendamento.Concluido)
            })
            .SingleOrDefaultAsync(cancellationToken);

        var itens = await validos
            .OrderBy(agendamento => agendamento.InicioUtc)
            .Take(limite)
            .Select(agendamento => new DashboardAgendamentoResultado(
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

        return new(totais?.Total ?? 0, totais?.Concluidos ?? 0, itens);
    }
}
