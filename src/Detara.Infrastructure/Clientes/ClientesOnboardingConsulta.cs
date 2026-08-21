using Detara.Application.Onboarding;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Clientes;

internal sealed class ClientesOnboardingConsulta(DetaraDbContext db)
    : IClientesOnboardingConsulta
{
    public async Task<EstadoClientesOnboarding> ObterEstadoAsync(
        Guid empresaId,
        CancellationToken cancellationToken)
    {
        var possuiCliente = await db.Clientes.AsNoTracking().AnyAsync(
            cliente => cliente.EmpresaId == empresaId && cliente.EhAtivo,
            cancellationToken);
        if (!possuiCliente)
        {
            return new(false, false);
        }

        var possuiClienteComVeiculo = await db.Veiculos.AsNoTracking().AnyAsync(
            veiculo => veiculo.EmpresaId == empresaId &&
                       veiculo.EhAtivo &&
                       db.Clientes.Any(cliente =>
                           cliente.EmpresaId == empresaId &&
                           cliente.Id == veiculo.ClienteId &&
                           cliente.EhAtivo),
            cancellationToken);
        return new(true, possuiClienteComVeiculo);
    }
}
