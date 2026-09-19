namespace Detara.UnitTests;

public sealed class Arch03ApresentacaoTests
{
    [Fact]
    public void Menu_VeiculosCombinaCapabilityComPermissao()
    {
        var menu = Ler("src", "Detara.Web", "Layout", "NavMenu.razor");
        Assert.Contains("Permissoes.VeiculosVisualizar", menu, StringComparison.Ordinal);
        Assert.Contains("Capacidades.Possui(CodigosCapacidadeContrato.Veiculos)", menu, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Veiculos.razor")]
    [InlineData("VeiculoDetalhe.razor")]
    [InlineData("VeiculoEdicao.razor")]
    public void RotasVeiculo_FalhamFechadasQuandoCapabilityEstaOff(string arquivo)
    {
        var pagina = Ler("src", "Detara.Web", "Pages", arquivo);
        Assert.Contains("Capacidades.Possui(CodigosCapacidadeContrato.Veiculos)", pagina, StringComparison.Ordinal);
        Assert.Contains("Módulo indisponível", pagina, StringComparison.Ordinal);
    }

    [Fact]
    public void CheckIn_CombinaCapabilityComPermissao()
    {
        var pagina = Ler("src", "Detara.Web", "Pages", "OrdemServicoDetalhe.razor");
        Assert.Contains("Permissoes.OrdemServicoEditar", pagina, StringComparison.Ordinal);
        Assert.Contains("CodigosCapacidadeContrato.CheckIn", pagina, StringComparison.Ordinal);
        Assert.Contains("Check-in indisponível", pagina, StringComparison.Ordinal);
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
