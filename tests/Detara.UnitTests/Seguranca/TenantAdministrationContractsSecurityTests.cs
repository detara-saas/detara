using Detara.Contracts.AdministracaoTenant;

namespace Detara.UnitTests.Seguranca;

public sealed class TenantAdministrationContractsSecurityTests
{
    [Fact]
    public void AtualizacaoEmpresa_AceitaSomenteCamposOperacionais()
    {
        Assert.Equal(
            ["CpfCnpj", "Email", "FusoHorario", "NomeFantasia", "RazaoSocial", "Telefone", "Versao"],
            typeof(AtualizarEmpresaTenantRequest).GetProperties()
                .Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Fact]
    public void ConviteUsuario_NaoAceitaSenhaOuTenantDoCliente()
    {
        Assert.Equal(
            ["Email", "Nome", "PerfilId"],
            typeof(ConvidarUsuarioTenantRequest).GetProperties()
                .Select(x => x.Name).OrderBy(x => x, StringComparer.Ordinal).ToArray());
    }

    [Theory]
    [InlineData(typeof(UsuarioTenantListaResponse))]
    [InlineData(typeof(UsuarioTenantDetalheResponse))]
    [InlineData(typeof(MinhaContaResponse))]
    public void RespostasDeIdentidade_NaoExibemSegredos(Type contrato)
    {
        var propriedades = contrato.GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(propriedades, x =>
            x.Contains("Senha", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Hash", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Token", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("VersaoSeguranca", StringComparison.OrdinalIgnoreCase));
    }
}
