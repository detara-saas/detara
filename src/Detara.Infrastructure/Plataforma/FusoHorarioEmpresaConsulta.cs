using Detara.Application.Agenda;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Plataforma;

internal sealed class FusoHorarioEmpresaConsulta(DetaraDbContext db) : IFusoHorarioEmpresaConsulta
{
    public Task<string?> ObterAsync(Guid empresaId, CancellationToken ct) => db.Empresas.AsNoTracking().Where(x => x.Id == empresaId).Select(x => x.FusoHorario).SingleOrDefaultAsync(ct);
}
