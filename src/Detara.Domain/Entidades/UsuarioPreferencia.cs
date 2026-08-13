namespace Detara.Domain.Entidades;

public sealed class UsuarioPreferencia : EntidadeEmpresaBase
{
    private UsuarioPreferencia()
    {
    }

    public UsuarioPreferencia(Guid empresaId, Guid usuarioId)
        : base(Guid.NewGuid(), empresaId)
    {
        UsuarioId = usuarioId == Guid.Empty
            ? throw new ArgumentException("O usuário deve ser informado.", nameof(usuarioId))
            : usuarioId;
        Tema = "Sistema";
        Idioma = "pt-BR";
        PaginaInicial = "dashboard";
    }

    public Guid UsuarioId { get; private set; }
    public string Tema { get; private set; } = "Sistema";
    public string Idioma { get; private set; } = "pt-BR";
    public bool SidebarRecolhida { get; private set; }
    public string PaginaInicial { get; private set; } = "dashboard";

    public void Atualizar(string tema, string idioma, bool sidebarRecolhida, string paginaInicial)
    {
        Tema = Exigir(tema, nameof(tema));
        Idioma = Exigir(idioma, nameof(idioma));
        PaginaInicial = Exigir(paginaInicial, nameof(paginaInicial));
        SidebarRecolhida = sidebarRecolhida;
        MarcarComoAtualizada();
    }

    private static string Exigir(string valor, string parametro) =>
        string.IsNullOrWhiteSpace(valor)
            ? throw new ArgumentException("O valor deve ser informado.", parametro)
            : valor.Trim();
}
