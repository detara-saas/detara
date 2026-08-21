using Detara.Application.Onboarding;
using Detara.Domain.Agenda;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Agenda;

internal sealed class AgendaOnboardingConsulta(DetaraDbContext db)
    : IAgendaOnboardingConsulta
{
    public Task<bool> PossuiAgendamentoValidoAsync(
        Guid empresaId,
        CancellationToken cancellationToken) =>
        db.Agendamentos.AsNoTracking().AnyAsync(
            agendamento => agendamento.EmpresaId == empresaId &&
                           agendamento.Status != StatusAgendamento.Cancelado &&
                           agendamento.Status != StatusAgendamento.NaoCompareceu,
            cancellationToken);
}
