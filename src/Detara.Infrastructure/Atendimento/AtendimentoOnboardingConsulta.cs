using Detara.Application.Onboarding;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Atendimento;

internal sealed class AtendimentoOnboardingConsulta(DetaraDbContext db)
    : IAtendimentoOnboardingConsulta
{
    public Task<bool> PossuiConfiguracaoOperacionalAsync(
        Guid empresaId,
        CancellationToken cancellationToken) =>
        db.ConfiguracoesOperacionaisAtendimento.AsNoTracking().AnyAsync(
            configuracao => configuracao.EmpresaId == empresaId,
            cancellationToken);
}
