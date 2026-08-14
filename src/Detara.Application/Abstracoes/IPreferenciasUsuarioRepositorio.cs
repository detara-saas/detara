using Detara.Domain.Entidades;

namespace Detara.Application.Abstracoes;

public interface IPreferenciasUsuarioRepositorio
{
    Task<UsuarioPreferencia?> ObterAsync(Guid usuarioId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UsuarioPaginaFavorita>> ObterFavoritosAsync(
        Guid usuarioPreferenciaId,
        CancellationToken cancellationToken);
    void Adicionar(UsuarioPreferencia preferencia);
    void SubstituirFavoritos(
        UsuarioPreferencia preferencia,
        IReadOnlyCollection<UsuarioPaginaFavorita> atuais,
        IReadOnlyCollection<string> paginas);
    Task SalvarAsync(CancellationToken cancellationToken);
}
