using Detara.Contracts.Plataforma;

namespace Detara.UnitTests.Seguranca;

public sealed class PlatformContractsSecurityTests
{
    [Fact]
    public void Provisionamento_AceitaSomenteCamposEditaveis()
    {
        Assert.Equal(
            [
                "AdministradorEmail",
                "AdministradorNome",
                "CpfCnpj",
                "EmailContato",
                "FusoHorario",
                "NomeFantasia",
                "RazaoSocial",
                "Telefone"
            ],
            typeof(ProvisionarEmpresaRequest).GetProperties()
                .Select(x => x.Name)
                .OrderBy(x => x, StringComparer.Ordinal)
                .ToArray());
    }

    [Theory]
    [InlineData(typeof(EmpresaPlataformaResumoResponse))]
    [InlineData(typeof(EmpresaPlataformaDetalheResponse))]
    public void ContratosPlataforma_NaoExibemDadosOperacionais(Type contrato)
    {
        var propriedades = contrato.GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain(propriedades, x =>
            x.Contains("Cliente", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Veiculo", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Agenda", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Orcamento", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("OrdemServico", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Financeiro", StringComparison.OrdinalIgnoreCase) ||
            x.Contains("Faturamento", StringComparison.OrdinalIgnoreCase));
    }
}
