namespace Detara.Application.Abstracoes;

public sealed record PaginacaoResultado<T>(
    IReadOnlyCollection<T> Itens,
    int Pagina,
    int TamanhoPagina,
    int TotalItens)
{
    public int TotalPaginas => TotalItens == 0
        ? 0
        : (int)Math.Ceiling(TotalItens / (double)TamanhoPagina);
}
