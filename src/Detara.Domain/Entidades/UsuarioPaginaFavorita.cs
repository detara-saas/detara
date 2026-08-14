namespace Detara.Domain.Entidades;

public sealed class UsuarioPaginaFavorita : EntidadeEmpresaBase
{
    private UsuarioPaginaFavorita()
    {
    }

    public UsuarioPaginaFavorita(
        Guid empresaId,
        Guid usuarioPreferenciaId,
        string pagina,
        int ordem)
        : base(Guid.NewGuid(), empresaId)
    {
        UsuarioPreferenciaId = usuarioPreferenciaId == Guid.Empty
            ? throw new ArgumentException("A preferência deve ser informada.", nameof(usuarioPreferenciaId))
            : usuarioPreferenciaId;
        Pagina = string.IsNullOrWhiteSpace(pagina)
            ? throw new ArgumentException("A página deve ser informada.", nameof(pagina))
            : pagina.Trim();
        Ordem = ordem < 0
            ? throw new ArgumentOutOfRangeException(nameof(ordem))
            : ordem;
    }

    public Guid UsuarioPreferenciaId { get; private set; }
    public string Pagina { get; private set; } = string.Empty;
    public int Ordem { get; private set; }
}
