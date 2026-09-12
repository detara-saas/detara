using System.Globalization;

namespace Detara.Web.Servicos;

public static class FormatacaoMoeda
{
    private static readonly CultureInfo CulturaBrl = CultureInfo.GetCultureInfo("pt-BR");

    public static string Brl(decimal valor) => valor.ToString("C2", CulturaBrl);
}
