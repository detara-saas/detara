namespace Detara.UnitTests;

public sealed class Subs012ApresentacaoTests
{
    [Fact]
    public void AssinaturaTenant_ExplicaTrialConfirmacaoEVencimentosSemInicioComercial()
    {
        var pagina = LerArquivo("src", "Detara.Web", "Pages", "Assinatura.razor");

        Assert.Contains("Início do teste", pagina);
        Assert.Contains("Fim do teste", pagina);
        Assert.Contains("Confirmação comercial", pagina);
        Assert.Contains("Primeiro vencimento", pagina);
        Assert.Contains("Após confirmação comercial", pagina);
        Assert.DoesNotContain("Início comercial", pagina);
    }

    [Fact]
    public void PlatformAdmin_ExibeAcaoExplicitaEAsQuatroDatas()
    {
        var componente = LerArquivo("src", "Detara.Web", "Components", "Plataforma",
            "AssinaturaPlataformaCard.razor");

        Assert.Contains("Início do teste", componente);
        Assert.Contains("Fim do teste", componente);
        Assert.Contains("Confirmação comercial", componente);
        Assert.Contains("Primeiro vencimento", componente);
        Assert.Contains("Confirmar contratação", componente);
        Assert.Contains("ConfirmarComercialmenteAsync", componente);
        Assert.DoesNotContain("Início comercial", componente);
    }

    private static string LerArquivo(params string[] partes) =>
        File.ReadAllText(Path.Combine([EncontrarRaizRepositorio(), .. partes]));

    private static string EncontrarRaizRepositorio()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Detara.sln")))
            atual = atual.Parent;
        return atual?.FullName ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
