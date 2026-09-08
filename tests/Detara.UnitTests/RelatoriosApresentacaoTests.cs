using System.Globalization;
using Detara.Web.Components.Relatorios;

namespace Detara.UnitTests;

public sealed class RelatoriosApresentacaoTests
{
    [Fact]
    public void CulturaPtBr_NaoContaminaCoordenadas()
    {
        var anterior = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("pt-BR");
            Assert.Equal("12.34", ApresentacaoRelatorio.Coordenada(12.34m));
            Assert.Contains("1.234,56", ApresentacaoRelatorio.Moeda(1234.56m));
            Assert.Equal("31,4%", ApresentacaoRelatorio.Percentual(31.4m));
            Assert.Contains("-10,0%", ApresentacaoRelatorio.Variacao(-10));
        }
        finally { CultureInfo.CurrentCulture = anterior; }
    }
}
