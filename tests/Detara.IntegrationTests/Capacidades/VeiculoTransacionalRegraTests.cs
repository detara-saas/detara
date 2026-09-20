using Detara.Application.Capacidades;
using Detara.IntegrationTests.Suporte;
using FluentValidation;

namespace Detara.IntegrationTests.Capacidades;

public sealed class VeiculoTransacionalRegraTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task GuidEmpty_NuncaEhAceitoComoVeiculo(bool utilizaVeiculos)
    {
        var excecao = await Assert.ThrowsAsync<ValidationException>(() =>
            VeiculoTransacionalRegra.ValidarAsync(new EmpresaCapacidadesTeste(utilizaVeiculos), Guid.Empty, default));

        Assert.Equal("VeiculoId", Assert.Single(excecao.Errors).PropertyName);
    }

    [Fact]
    public async Task SemVeiculos_AceitaAusenciaDeVeiculo()
    {
        var resultado = await VeiculoTransacionalRegra.ValidarAsync(
            new EmpresaCapacidadesTeste(false),
            null,
            default);

        Assert.False(resultado);
    }
}
