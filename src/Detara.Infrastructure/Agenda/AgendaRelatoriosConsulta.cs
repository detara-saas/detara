using Detara.Application.Relatorios;
using Detara.Domain.Agenda;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Agenda;

internal sealed class AgendaRelatoriosConsulta(DetaraDbContext db) : IAgendaRelatoriosConsulta
{
    public async Task<AgendaRelatorio> ObterAsync(Guid empresaId, IntervaloRelatorio p, CancellationToken ct)
    {
        var grupos = await db.Agendamentos.AsNoTracking().Where(x => x.EmpresaId == empresaId &&
            x.InicioUtc >= p.InicioUtc && x.InicioUtc < p.FimExclusivoUtc)
            .GroupBy(x => x.Status).Select(g => new { Status = g.Key, Quantidade = g.Count() }).ToArrayAsync(ct);
        return new(grupos.Sum(x => x.Quantidade), grupos.Where(x => x.Status == StatusAgendamento.Cancelado).Sum(x => x.Quantidade),
            grupos.Where(x => x.Status == StatusAgendamento.NaoCompareceu).Sum(x => x.Quantidade));
    }
}
