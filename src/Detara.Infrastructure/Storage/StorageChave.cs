namespace Detara.Infrastructure.Storage;

internal static class StorageChave
{
    public static void Validar(string chave)
    {
        if (string.IsNullOrWhiteSpace(chave) ||
            Path.IsPathRooted(chave) ||
            chave.Contains('\\'))
        {
            throw new ArgumentException("A chave de storage é inválida.", nameof(chave));
        }

        var segmentos = chave.Split('/', StringSplitOptions.None);
        if (segmentos.Length == 0 || segmentos.Any(segmento =>
                string.IsNullOrWhiteSpace(segmento) ||
                segmento is "." or ".." ||
                segmento.Any(char.IsControl)))
        {
            throw new ArgumentException("A chave de storage é inválida.", nameof(chave));
        }
    }
}
