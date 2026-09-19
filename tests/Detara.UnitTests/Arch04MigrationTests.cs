namespace Detara.UnitTests;

public sealed class Arch04MigrationTests
{
    [Fact]
    public void Migration_TornaSomenteVeiculoDoBackboneNullable()
    {
        var raiz = EncontrarRaiz();
        var migration = Directory.GetFiles(
                Path.Combine(raiz, "src", "Detara.Infrastructure", "Persistencia", "Migrations"),
                "*MakeVehicleOptionalInTransactionalBackbone.cs")
            .Single();
        var texto = File.ReadAllText(migration);

        foreach (var tabela in new[] { "Agendamentos", "Orcamentos", "OrdensServico", "ContasReceber" })
        {
            Assert.Contains($"table: \"{tabela}\"", texto, StringComparison.Ordinal);
            Assert.Equal(4, Contar(texto, $"table: \"{tabela}\""));
        }

        Assert.Equal(8, Contar(texto, "name: \"VeiculoId\""));
        Assert.Equal(8, Contar(texto, "name: \"VeiculoDescricaoSnapshot\""));
        Assert.DoesNotContain("DropForeignKey", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("DropTable", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("00000000-0000-0000-0000-000000000000", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("defaultValue:", texto, StringComparison.Ordinal);
        Assert.Contains("Não é possível reverter a migration enquanto houver transações sem veículo.", texto, StringComparison.Ordinal);
    }

    private static int Contar(string texto, string trecho) =>
        texto.Split(trecho, StringSplitOptions.None).Length - 1;

    private static string EncontrarRaiz()
    {
        var atual = new DirectoryInfo(AppContext.BaseDirectory);
        while (atual is not null && !File.Exists(Path.Combine(atual.FullName, "Detara.sln"))) atual = atual.Parent;
        return atual?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
