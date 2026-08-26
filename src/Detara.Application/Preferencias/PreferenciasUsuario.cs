using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using MediatR;

namespace Detara.Application.Preferencias;

public sealed record PreferenciasUsuarioResultado(
    string Tema,
    string Idioma,
    bool SidebarRecolhida,
    string PaginaInicial,
    IReadOnlyCollection<string> Favoritos);

public sealed record ObterPreferenciasUsuarioQuery : IRequest<PreferenciasUsuarioResultado>;

public sealed record AtualizarPreferenciasUsuarioCommand(
    string Tema,
    string Idioma,
    bool SidebarRecolhida,
    string PaginaInicial,
    IReadOnlyCollection<string> Favoritos) : IRequest<PreferenciasUsuarioResultado>;

internal sealed class ObterPreferenciasUsuarioHandler(
    IUsuarioContexto usuarioContexto,
    IPreferenciasUsuarioRepositorio repositorio)
    : IRequestHandler<ObterPreferenciasUsuarioQuery, PreferenciasUsuarioResultado>
{
    public async Task<PreferenciasUsuarioResultado> Handle(
        ObterPreferenciasUsuarioQuery request,
        CancellationToken cancellationToken)
    {
        var preferencia = await repositorio.ObterAsync(usuarioContexto.UsuarioId, cancellationToken);
        if (preferencia is null)
        {
            return PreferenciasUsuarioPadrao.Criar();
        }

        var favoritos = await repositorio.ObterFavoritosAsync(preferencia.Id, cancellationToken);
        return Mapear(preferencia, favoritos);
    }

    internal static PreferenciasUsuarioResultado Mapear(
        UsuarioPreferencia preferencia,
        IEnumerable<UsuarioPaginaFavorita> favoritos) =>
        new(
            preferencia.Tema,
            preferencia.Idioma,
            preferencia.SidebarRecolhida,
            preferencia.PaginaInicial,
            favoritos.OrderBy(item => item.Ordem).Select(item => item.Pagina).ToArray());
}

internal sealed class AtualizarPreferenciasUsuarioHandler(
    IUsuarioContexto usuarioContexto,
    IPreferenciasUsuarioRepositorio repositorio)
    : IRequestHandler<AtualizarPreferenciasUsuarioCommand, PreferenciasUsuarioResultado>
{
    public async Task<PreferenciasUsuarioResultado> Handle(
        AtualizarPreferenciasUsuarioCommand request,
        CancellationToken cancellationToken)
    {
        Validar(request);
        var preferencia = await repositorio.ObterAsync(usuarioContexto.UsuarioId, cancellationToken);
        if (preferencia is null)
        {
            preferencia = new UsuarioPreferencia(usuarioContexto.EmpresaId, usuarioContexto.UsuarioId);
            repositorio.Adicionar(preferencia);
        }

        preferencia.Atualizar(
            request.Tema,
            request.Idioma,
            request.SidebarRecolhida,
            request.PaginaInicial);
        var atuais = await repositorio.ObterFavoritosAsync(preferencia.Id, cancellationToken);
        repositorio.SubstituirFavoritos(preferencia, atuais, request.Favoritos);
        await repositorio.SalvarAsync(cancellationToken);

        var favoritos = await repositorio.ObterFavoritosAsync(preferencia.Id, cancellationToken);
        return ObterPreferenciasUsuarioHandler.Mapear(preferencia, favoritos);
    }

    private static void Validar(AtualizarPreferenciasUsuarioCommand request)
    {
        if (request.Tema is not ("Sistema" or "Claro" or "Escuro"))
        {
            throw new ArgumentException("Tema inválido.", nameof(request.Tema));
        }

        if (!string.Equals(request.Idioma, "pt-BR", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Idioma ainda não suportado.", nameof(request.Idioma));
        }

        if (request.Favoritos is null ||
            request.Favoritos.Count > 12 ||
            !PaginasDetara.PaginasIniciaisPermitidas.Contains(request.PaginaInicial) ||
            request.Favoritos.Any(pagina => !PaginasDetara.Permitidas.Contains(pagina)) ||
            request.Favoritos.Count != request.Favoritos.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            throw new ArgumentException("Página de navegação inválida.");
        }
    }
}

internal static class PreferenciasUsuarioPadrao
{
    public static PreferenciasUsuarioResultado Criar() =>
        new("Sistema", "pt-BR", false, PaginasDetara.Dashboard, []);
}
