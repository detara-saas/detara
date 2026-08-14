namespace Detara.Contracts.Comum;

public sealed record PaginaResponse<T>(
    IReadOnlyCollection<T> Itens,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);
