using Detara.Web.Components.Agenda;

namespace Detara.UnitTests.Agenda;

public sealed class EstadoDuracaoPlanejadaTests
{
    [Fact]
    public void ModoAutomatico_AcompanhaAdicaoERemocaoDeItens()
    {
        var estado = new EstadoDuracaoPlanejada();

        estado.AtualizarSugestao(240, true);
        Assert.Equal(240, estado.Valor);

        estado.AtualizarSugestao(330, true);
        Assert.Equal(330, estado.Valor);

        estado.AtualizarSugestao(240, true);
        Assert.Equal(240, estado.Valor);
    }

    [Fact]
    public void OverrideManual_NaoEhSobrescritoPorNovaSugestao()
    {
        var estado = new EstadoDuracaoPlanejada();
        estado.AtualizarSugestao(240, true);

        estado.Personalizar(300);
        estado.AtualizarSugestao(330, true);

        Assert.True(estado.Personalizada);
        Assert.Equal(300, estado.Valor);
    }

    [Fact]
    public void UsarSugestao_RestauraAcompanhamentoAutomatico()
    {
        var estado = new EstadoDuracaoPlanejada();
        estado.Personalizar(300);

        estado.UsarSugestao(240);
        estado.AtualizarSugestao(330, true);

        Assert.False(estado.Personalizada);
        Assert.Equal(330, estado.Valor);
    }

    [Fact]
    public void EdicaoExistente_PreservaValorPersistido()
    {
        var estado = new EstadoDuracaoPlanejada();
        estado.InicializarExistente(300);

        estado.AtualizarSugestao(240, true);

        Assert.True(estado.Personalizada);
        Assert.Equal(300, estado.Valor);
    }

    [Fact]
    public void SemSugestao_NaoInventaDuracaoEListaVaziaLimpaModoAutomatico()
    {
        var estado = new EstadoDuracaoPlanejada();
        estado.AtualizarSugestao(null, true);
        Assert.Null(estado.Valor);

        estado.AtualizarSugestao(240, true);
        estado.AtualizarSugestao(null, false);
        Assert.Null(estado.Valor);
    }
}
