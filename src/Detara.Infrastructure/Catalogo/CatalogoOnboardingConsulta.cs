using Detara.Application.Onboarding;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Catalogo;

internal sealed class CatalogoOnboardingConsulta(DetaraDbContext db)
    : ICatalogoOnboardingConsulta
{
    public Task<bool> PossuiServicoAtivoAsync(
        Guid empresaId,
        CancellationToken cancellationToken) =>
        db.Servicos.AsNoTracking().AnyAsync(
            servico => servico.EmpresaId == empresaId && servico.EhAtivo,
            cancellationToken);
}
