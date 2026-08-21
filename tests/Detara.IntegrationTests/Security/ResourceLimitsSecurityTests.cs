using Detara.Application.Atendimento;
using Detara.Application.Financeiro;
using Detara.Application.Plataforma;
using Detara.Application.AdministracaoTenant;

namespace Detara.IntegrationTests.Security;

public sealed class ResourceLimitsSecurityTests
{
    [Theory]
    [InlineData(10, true)]
    [InlineData(25, true)]
    [InlineData(50, true)]
    [InlineData(20, false)]
    [InlineData(int.MaxValue, false)]
    public void ListagensPlataforma_AceitamSomenteTamanhosDePaginaPermitidos(
        int tamanhoPagina,
        bool esperadoValido)
    {
        var empresas = new ListarEmpresasPlataformaValidator().Validate(
            new ListarEmpresasPlataformaQuery(1, tamanhoPagina, null, null));
        var auditoria = new ListarAuditoriaPlataformaValidator().Validate(
            new ListarAuditoriaPlataformaQuery(1, tamanhoPagina, null, null, null, null));

        Assert.Equal(esperadoValido, empresas.IsValid);
        Assert.Equal(esperadoValido, auditoria.IsValid);
    }

    [Theory]
    [InlineData(10, true)]
    [InlineData(25, true)]
    [InlineData(50, true)]
    [InlineData(20, false)]
    [InlineData(int.MaxValue, false)]
    public void ListagemUsuariosTenant_AceitaSomenteWhitelistComDefault25(
        int tamanhoPagina,
        bool esperadoValido)
    {
        var query = new ListarUsuariosTenantQuery(TamanhoPagina: tamanhoPagina);

        Assert.Equal(esperadoValido, new ListarUsuariosTenantValidator().Validate(query).IsValid);
        Assert.Equal(25, new ListarUsuariosTenantQuery().TamanhoPagina);
    }

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
