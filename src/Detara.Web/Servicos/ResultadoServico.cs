namespace Detara.Web.Servicos;

public sealed record ResultadoServico<T>(bool Sucesso, T? Resultado, string Mensagem)
{
    public static ResultadoServico<T> Ok(T resultado, string mensagem = "Operação realizada com sucesso.") =>
        new(true, resultado, mensagem);

    public static ResultadoServico<T> Falha(string mensagem) => new(false, default, mensagem);
}
