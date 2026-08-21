using Detara.Application.Onboarding;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Plataforma;

internal sealed class PlataformaOnboardingConsulta(DetaraDbContext db)
    : IPlataformaOnboardingConsulta
{
    public Task<bool> PossuiEmpresaConfiguradaAsync(
        Guid empresaId,
        CancellationToken cancellationToken) =>
        db.Empresas.AsNoTracking().AnyAsync(
            empresa => empresa.Id == empresaId &&
                       empresa.EhAtivo &&
                       empresa.NomeFantasia != string.Empty &&
                       empresa.RazaoSocial != string.Empty &&
                       empresa.CpfCnpj != string.Empty &&
                       empresa.Slug != string.Empty &&
                       empresa.FusoHorario != string.Empty,
            cancellationToken);
}
