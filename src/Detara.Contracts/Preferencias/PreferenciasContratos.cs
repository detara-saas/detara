namespace Detara.Contracts.Preferencias;

public sealed record PreferenciasUsuarioResponse(
    string Tema,
    string Idioma,
    bool SidebarRecolhida,
    string PaginaInicial,
    IReadOnlyCollection<string> Favoritos);

public sealed record AtualizarPreferenciasUsuarioRequest(
    string Tema,
    string Idioma,
    bool SidebarRecolhida,
    string PaginaInicial,
    IReadOnlyCollection<string> Favoritos);
