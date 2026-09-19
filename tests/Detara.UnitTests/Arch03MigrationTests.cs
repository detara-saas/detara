namespace Detara.UnitTests;

public sealed class Arch03MigrationTests
{
    [Fact]
    public void Migration_IncluiBackfillCompletoFkRestritivaEIndiceUnico()
    {
        var raiz = EncontrarRaiz();
        var migration = Directory.GetFiles(
                Path.Combine(raiz, "src", "Detara.Infrastructure", "Persistencia", "Migrations"),
                "*AddCompanyCapabilities.cs")
            .Single();
        var texto = File.ReadAllText(migration);

        foreach (var codigo in new[] { "clientes", "servicos", "pacotes", "agenda", "orcamentos",
                     "ordem-servico", "financeiro", "despesas", "veiculos", "check-in" })
            Assert.Contains($"(N'{codigo}')", texto, StringComparison.Ordinal);
        Assert.Contains("unique: true", texto, StringComparison.Ordinal);
        Assert.Contains("ReferentialAction.Restrict", texto, StringComparison.Ordinal);
        Assert.Contains("WHERE NOT EXISTS", texto, StringComparison.Ordinal);
    }

    private static string EncontrarRaiz()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Detara.sln"))) atual = atual.Parent;
        return atual?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
