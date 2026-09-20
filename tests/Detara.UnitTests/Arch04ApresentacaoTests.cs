namespace Detara.UnitTests;

public sealed class Arch04ApresentacaoTests
{
    [Theory]
    [InlineData("Components", "Agenda", "AgendamentoFormulario.razor")]
    [InlineData("Components", "Atendimento", "OrcamentoFormulario.razor")]
    [InlineData("Pages", "OrdemServicoNova.razor")]
    public void FormulariosTransacionais_CompoemVeiculoPelaCapability(params string[] caminho)
    {
        var pagina = Ler(["src", "Detara.Web", .. caminho]);

        Assert.Contains("CodigosCapacidadeContrato.Veiculos", pagina, StringComparison.Ordinal);
        Assert.Contains("UsaVeiculos", pagina, StringComparison.Ordinal);
        Assert.Contains("VeiculoId", pagina, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Agenda.razor")]
    [InlineData("Orcamentos.razor")]
    [InlineData("OrdensServico.razor")]
    [InlineData("ContasReceber.razor")]
    public void Listagens_OmitemColunaAutomotivaQuandoCapabilityEstaOff(string arquivo)
    {
        var pagina = Ler("src", "Detara.Web", "Pages", arquivo);

        Assert.Contains("@if (UsaVeiculos)", pagina, StringComparison.Ordinal);
        Assert.Contains("CodigosCapacidadeContrato.Veiculos", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformAdmin_AssinaturaOcupaGridCompletoECapacidadesTemRitmoVertical()
    {
        var pagina = Ler("src", "Detara.Web", "Pages", "PlatformEmpresaDetalhe.razor");
        var estilos = Ler("src", "Detara.Web", "wwwroot", "css", "app.css");
        var assinatura = Ler("src", "Detara.Web", "Components", "Plataforma",
            "AssinaturaPlataformaCard.razor.css");

        Assert.Contains("platform-subscription-slot", pagina, StringComparison.Ordinal);
        Assert.Contains("platform-capabilities-panel", pagina, StringComparison.Ordinal);
        Assert.Contains("grid-column: 1 / -1", estilos, StringComparison.Ordinal);
        Assert.Contains("margin-top: var(--detara-section-gap)", estilos, StringComparison.Ordinal);
        Assert.Contains("gap: var(--detara-form-gap)", assinatura, StringComparison.Ordinal);
    }

    [Fact]
    public void PlatformAdmin_MantemCapacidadesSomenteLeitura()
    {
        var pagina = Ler("src", "Detara.Web", "Pages", "PlatformEmpresaDetalhe.razor");

        Assert.Contains("alterações estão bloqueadas nesta fase", pagina, StringComparison.Ordinal);
        Assert.DoesNotContain("MudSwitch", pagina, StringComparison.Ordinal);
    }

    private static string Ler(params string[] partes) =>
        File.ReadAllText(Path.Combine([EncontrarRaiz(), .. partes]));

    private static string EncontrarRaiz()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Detara.sln"))) atual = atual.Parent;
        return atual?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
