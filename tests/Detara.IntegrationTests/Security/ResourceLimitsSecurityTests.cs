using Detara.Application.Atendimento;
using Detara.Application.Financeiro;

namespace Detara.IntegrationTests.Security;

public sealed class ResourceLimitsSecurityTests
{
    [Fact]
    public void ListagemDeOrdensServico_RejeitaPeriodoSuperiorADezAnos()
    {
        var inicio = new DateOnly(2010, 1, 1);
        var resultado = new ListarOrdensServicoValidator().Validate(
            new ListarOrdensServicoQuery(1, 25, null, inicio, inicio.AddDays(3661), null));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, erro => erro.ErrorMessage.Contains("dez anos"));
    }

    [Fact]
    public void ListagemFinanceira_RejeitaPeriodoSuperiorADezAnos()
    {
        var inicio = new DateOnly(2010, 1, 1);
        var resultado = new ListarContasReceberValidator().Validate(
            new ListarContasReceberQuery(1, 25, null, null, inicio, inicio.AddDays(3661), null));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, erro => erro.ErrorMessage.Contains("dez anos"));
    }

    [Fact]
    public void ResumoFinanceiro_RejeitaPeriodoSuperiorADezAnos()
    {
        var inicio = new DateOnly(2010, 1, 1);
        var resultado = new ObterResumoFinanceiroValidator().Validate(
            new ObterResumoFinanceiroQuery(inicio, inicio.AddDays(3661)));

        Assert.False(resultado.IsValid);
        Assert.Contains(resultado.Errors, erro => erro.ErrorMessage.Contains("dez anos"));
    }
}
