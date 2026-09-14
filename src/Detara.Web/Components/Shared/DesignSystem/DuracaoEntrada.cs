namespace Detara.Web.Components.Shared.DesignSystem;

internal enum UnidadeDuracao
{
    Minutos,
    Horas,
    Dias
}

internal static class DuracaoEntrada
{
    public static UnidadeDuracao SugerirUnidade(int minutos) => minutos switch
    {
        >= 1440 => UnidadeDuracao.Dias,
        >= 60 => UnidadeDuracao.Horas,
        _ => UnidadeDuracao.Minutos
    };

    public static decimal ParaQuantidade(int minutos, UnidadeDuracao unidade) =>
        minutos / (decimal)Fator(unidade);

    public static int ParaMinutos(decimal quantidade, UnidadeDuracao unidade) => checked((int)Math.Round(
        quantidade * Fator(unidade),
        MidpointRounding.AwayFromZero));

    public static string Formatar(int minutos)
    {
        if (minutos < 60) return $"{minutos} min";

        var dias = minutos / 1440;
        var horas = minutos % 1440 / 60;
        var minutosRestantes = minutos % 60;
        var partes = new List<string>(3);
        if (dias > 0) partes.Add($"{dias} {(dias == 1 ? "dia" : "dias")}");
        if (horas > 0) partes.Add($"{horas} h");
        if (minutosRestantes > 0) partes.Add($"{minutosRestantes} min");
        return string.Join(" ", partes);
    }

    public static int Fator(UnidadeDuracao unidade) => unidade switch
    {
        UnidadeDuracao.Minutos => 1,
        UnidadeDuracao.Horas => 60,
        UnidadeDuracao.Dias => 1440,
        _ => throw new ArgumentOutOfRangeException(nameof(unidade), unidade, null)
    };
}
