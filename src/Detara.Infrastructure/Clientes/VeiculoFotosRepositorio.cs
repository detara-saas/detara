using Detara.Application.Clientes;
using Detara.Domain.Clientes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Clientes;

internal sealed class VeiculoFotosRepositorio(DetaraDbContext dbContext)
    : IVeiculoFotosRepositorio
{
    public Task<Veiculo?> ObterVeiculoAsync(
        Guid veiculoId,
        CancellationToken cancellationToken) =>
        dbContext.Veiculos
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == veiculoId, cancellationToken);

    public async Task<IReadOnlyCollection<VeiculoFoto>> ListarAsync(
        Guid veiculoId,
        CancellationToken cancellationToken) =>
        await dbContext.VeiculosFotos
            .AsNoTracking()
            .Where(item => item.VeiculoId == veiculoId)
            .OrderByDescending(item => item.EhPrincipal)
            .ThenBy(item => item.CriadoEmUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<VeiculoFoto>> ListarParaAlteracaoAsync(
        Guid veiculoId,
        CancellationToken cancellationToken) =>
        await dbContext.VeiculosFotos
            .Where(item => item.VeiculoId == veiculoId)
            .OrderBy(item => item.CriadoEmUtc)
            .ThenBy(item => item.Id)
            .ToArrayAsync(cancellationToken);

    public Task<VeiculoFoto?> ObterAsync(
        Guid veiculoId,
        Guid fotoId,
        bool paraAlteracao,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.VeiculosFotos
            .Where(item => item.VeiculoId == veiculoId && item.Id == fotoId);
        return (paraAlteracao ? consulta : consulta.AsNoTracking())
            .SingleOrDefaultAsync(cancellationToken);
    }

    public void Adicionar(VeiculoFoto foto) => dbContext.VeiculosFotos.Add(foto);
    public void Remover(VeiculoFoto foto) => dbContext.VeiculosFotos.Remove(foto);
    public Task SalvarAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
