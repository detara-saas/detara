namespace Detara.Domain.Catalogo;

public enum TipoPrecificacao
{
    Fixo = 1,
    APartirDe = 2,
    SobConsulta = 3
}

internal static class PrecificacaoCatalogo
{
    public static decimal? Validar(TipoPrecificacao tipo, decimal? preco, string parametro)
    {
        if (!Enum.IsDefined(tipo))
        {
            throw new ArgumentException("O tipo de precificação é inválido.", nameof(tipo));
        }

        if (tipo == TipoPrecificacao.SobConsulta)
        {
            if (preco.HasValue)
            {
                throw new ArgumentException("Itens sob consulta não podem possuir preço de referência.", parametro);
            }

            return null;
        }

        if (!preco.HasValue || preco.Value < 0)
        {
            throw new ArgumentException("O preço de referência deve ser informado e não pode ser negativo.", parametro);
        }

        return preco.Value;
    }
}
