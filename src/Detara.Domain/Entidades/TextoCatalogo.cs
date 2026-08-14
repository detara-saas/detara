namespace Detara.Domain.Entidades;

internal static class TextoCatalogo
{
    public static string Exigir(string valor, int limite, string parametro, int minimo = 1)
    {
        var normalizado = string.IsNullOrWhiteSpace(valor) ? string.Empty : valor.Trim();
        if (normalizado.Length < minimo || normalizado.Length > limite)
        {
            throw new ArgumentException($"O valor deve possuir entre {minimo} e {limite} caracteres.", parametro);
        }

        return normalizado;
    }

    public static string? NormalizarOpcional(string? valor, int limite)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var normalizado = valor.Trim();
        return normalizado.Length <= limite
            ? normalizado
            : throw new ArgumentException($"O valor deve possuir no máximo {limite} caracteres.");
    }
}
