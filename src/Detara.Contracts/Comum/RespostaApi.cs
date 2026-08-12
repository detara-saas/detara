namespace Detara.Contracts.Comum;

public sealed record ErroApi(string Codigo, IReadOnlyDictionary<string, string[]>? Detalhes = null);

public sealed record RespostaApi<T>(bool Sucesso, string Info, T? Resultado, ErroApi? Erro)
{
    public static RespostaApi<T> Ok(T resultado, string info = "Operação realizada com sucesso.") =>
        new(true, info, resultado, null);

    public static RespostaApi<T> Falha(
        string info,
        string codigo,
        IReadOnlyDictionary<string, string[]>? detalhes = null) =>
        new(false, info, default, new ErroApi(codigo, detalhes));
}
