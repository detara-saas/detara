using Detara.Application.Dashboard;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Plataforma;

internal sealed class PlataformaDashboardConsulta(DetaraDbContext db)
    : IPlataformaDashboardConsulta
{
    public Task<string?> ObterFusoHorarioAsync(
        Guid empresaId,
        CancellationToken cancellationToken) =>
        db.Empresas
            .AsNoTracking()
            .Where(empresa => empresa.Id == empresaId)
            .Select(empresa => empresa.FusoHorario)
            .SingleOrDefaultAsync(cancellationToken);
}
