using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Preferencias;

internal sealed class PreferenciasUsuarioRepositorio(DetaraDbContext dbContext)
    : IPreferenciasUsuarioRepositorio
{
    public Task<UsuarioPreferencia?> ObterAsync(
        Guid usuarioId,
        CancellationToken cancellationToken) =>
        dbContext.UsuariosPreferencias.SingleOrDefaultAsync(
            preferencia => preferencia.UsuarioId == usuarioId,
            cancellationToken);

    public async Task<IReadOnlyCollection<UsuarioPaginaFavorita>> ObterFavoritosAsync(
        Guid usuarioPreferenciaId,
        CancellationToken cancellationToken) =>
        await dbContext.UsuariosPaginasFavoritas
            .Where(item => item.UsuarioPreferenciaId == usuarioPreferenciaId)
            .OrderBy(item => item.Ordem)
            .ToArrayAsync(cancellationToken);

    public void Adicionar(UsuarioPreferencia preferencia) =>
        dbContext.UsuariosPreferencias.Add(preferencia);

    public void SubstituirFavoritos(
        UsuarioPreferencia preferencia,
        IReadOnlyCollection<UsuarioPaginaFavorita> atuais,
        IReadOnlyCollection<string> paginas)
    {
        dbContext.UsuariosPaginasFavoritas.RemoveRange(atuais);
        dbContext.UsuariosPaginasFavoritas.AddRange(paginas.Select((pagina, ordem) =>
            new UsuarioPaginaFavorita(
                preferencia.EmpresaId,
                preferencia.Id,
                pagina.ToLowerInvariant(),
                ordem)));
    }

    public Task SalvarAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
