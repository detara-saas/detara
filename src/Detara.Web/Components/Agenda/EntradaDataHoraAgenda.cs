using System.Globalization;
using MudBlazor;

namespace Detara.Web.Components.Agenda;

public static class EntradaDataHoraAgenda
{
    private const string FormatoData = "dd/MM/yyyy";
    public static CultureInfo Cultura { get; } = CultureInfo.GetCultureInfo("pt-BR");

    public static IMask CriarMascaraData() => new PatternMask("00/00/0000");

    public static IMask CriarMascaraHora() => new PatternMask("00:00");

    public static bool TentarInterpretarData(string? texto, out DateTime data)
    {
        var normalizado = Normalizar(texto, 8, 2, 2);
        return DateTime.TryParseExact(normalizado, FormatoData, Cultura,
            DateTimeStyles.None, out data);
    }

    public static bool TentarInterpretarHora(string? texto, out TimeSpan hora)
    {
        var normalizado = Normalizar(texto, 4, 2);
        return TimeSpan.TryParseExact(normalizado, @"hh\:mm", CultureInfo.InvariantCulture,
            out hora) && hora >= TimeSpan.Zero && hora < TimeSpan.FromDays(1);
    }

    public static (DateTime Data, TimeSpan Hora) ResolverPadrao(
        DateOnly hojeLocal,
        DateTime agoraLocal,
        DateTime? dataInicial = null,
        TimeSpan? horaInicial = null)
        => (dataInicial?.Date ?? hojeLocal.ToDateTime(TimeOnly.MinValue),
            horaInicial ?? new TimeSpan(agoraLocal.Hour, agoraLocal.Minute, 0));

    private static string Normalizar(string? texto, int quantidadeDigitos, params int[] grupos)
    {
        if (string.IsNullOrWhiteSpace(texto)) return string.Empty;
        var digitos = new string(texto.Where(char.IsDigit).ToArray());
        if (digitos.Length != quantidadeDigitos) return texto.Trim();

        var partes = new List<string>(grupos.Length + 1);
        var inicio = 0;
        foreach (var tamanho in grupos)
        {
            partes.Add(digitos.Substring(inicio, tamanho));
            inicio += tamanho;
        }
        partes.Add(digitos[inicio..]);
        return string.Join(grupos.Length == 1 ? ':' : '/', partes);
    }
}
