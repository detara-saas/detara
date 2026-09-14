using Detara.Web.Components.Shared.DesignSystem;

namespace Detara.UnitTests;

public sealed class Ux08ApresentacaoTests
{
    [Theory]
    [InlineData(45, 0, 45)]
    [InlineData(90, 1, 1.5)]
    [InlineData(4320, 2, 3)]
    public void Duracao_SugereUnidadeLegivelEPreservaMinutos(
        int minutos,
        int unidadeValor,
        decimal quantidade)
    {
        var unidade = (UnidadeDuracao)unidadeValor;
        Assert.Equal(unidade, DuracaoEntrada.SugerirUnidade(minutos));
        Assert.Equal(quantidade, DuracaoEntrada.ParaQuantidade(minutos, unidade));
        Assert.Equal(minutos, DuracaoEntrada.ParaMinutos(quantidade, unidade));
    }

    [Theory]
    [InlineData(45, "45 min")]
    [InlineData(90, "1 h 30 min")]
    [InlineData(1440, "1 dia")]
    [InlineData(2940, "2 dias 1 h")]
    public void Duracao_FormataPeriodosLongos(int minutos, string esperado) =>
        Assert.Equal(esperado, DuracaoEntrada.Formatar(minutos));

    [Fact]
    public void EntradasDeDuracao_CompartilhamSeletorDeMinutosHorasEDias()
    {
        var arquivos = new[]
        {
            LerArquivo("src", "Detara.Web", "Components", "Catalogo", "ServicoFormulario.razor"),
            LerArquivo("src", "Detara.Web", "Components", "Agenda", "AgendamentoFormulario.razor"),
            LerArquivo("src", "Detara.Web", "Pages", "AgendamentoReagendar.razor"),
            LerArquivo("src", "Detara.Web", "Pages", "OrdemServicoNova.razor")
        };
        var campo = LerArquivo("src", "Detara.Web", "Components", "Shared", "DesignSystem",
            "CampoDuracao.razor");

        Assert.All(arquivos, arquivo => Assert.Contains("<CampoDuracao", arquivo));
        Assert.Contains("UnidadeDuracao.Minutos", campo);
        Assert.Contains("UnidadeDuracao.Horas", campo);
        Assert.Contains("UnidadeDuracao.Dias", campo);
        Assert.Contains("ValorMinutosChanged.InvokeAsync", campo);
    }

    [Fact]
    public void PrimeiroServico_ExibeEmptyStateSemRenderizarGuidEmpty()
    {
        var formulario = LerArquivo("src", "Detara.Web", "Components", "Catalogo",
            "ServicoFormulario.razor");

        Assert.Contains("Nenhuma categoria cadastrada", formulario);
        Assert.Contains("Para cadastrar seu primeiro serviço, crie uma categoria primeiro.", formulario);
        Assert.Contains("Cadastrar categoria", formulario);
        Assert.Contains("OnClick=\"CriarCategoriaRapidaAsync\"", formulario);
        Assert.Contains("MudSelect T=\"Guid?\"", formulario);
        Assert.DoesNotContain("MudSelect T=\"Guid\"", formulario);
        Assert.DoesNotContain("?? Guid.Empty", formulario);
    }

    [Fact]
    public void CriacaoRapida_RecarregaSelecionaEPreservaFormularioDoServico()
    {
        var formulario = LerArquivo("src", "Detara.Web", "Components", "Catalogo",
            "ServicoFormulario.razor");
        var dialogo = LerArquivo("src", "Detara.Web", "Components", "Catalogo",
            "CategoriaServicoRapidaDialog.razor");
        var inicio = formulario.IndexOf("private async Task CriarCategoriaRapidaAsync()",
            StringComparison.Ordinal);
        var fim = formulario.IndexOf("private void AlterarTipoPrecificacao", inicio,
            StringComparison.Ordinal);
        var criacaoRapida = formulario[inicio..fim];

        Assert.Contains("Catalogo.CriarCategoriaAsync", dialogo);
        Assert.Contains("SalvarCategoriaServicoRequest", dialogo);
        Assert.Contains("await CarregarCategoriasAsync();", criacaoRapida);
        Assert.Contains("_categoriaId = categoria.Id", criacaoRapida);
        Assert.DoesNotContain("_nome =", criacaoRapida);
        Assert.DoesNotContain("_descricao =", criacaoRapida);
        Assert.DoesNotContain("_preco =", criacaoRapida);
        Assert.DoesNotContain("_duracao =", criacaoRapida);
    }

    [Fact]
    public void CategoriasExistentes_MantemSelectEValidacaoEspecifica()
    {
        var formulario = LerArquivo("src", "Detara.Web", "Components", "Catalogo",
            "ServicoFormulario.razor");

        Assert.Contains("else", formulario);
        Assert.Contains("@foreach (var categoria in CategoriasDisponiveis)", formulario);
        Assert.Contains("RequiredError=\"Selecione uma categoria para o serviço.\"", formulario);
    }

    private static string LerArquivo(params string[] partes)
        => File.ReadAllText(Path.Combine([EncontrarRaizRepositorio(), .. partes]));

    private static string EncontrarRaizRepositorio()
    {
        var diretorio = new DirectoryInfo(AppContext.BaseDirectory);
        while (diretorio is not null && !File.Exists(Path.Combine(diretorio.FullName, "Detara.sln")))
            diretorio = diretorio.Parent;
        return diretorio?.FullName
            ?? throw new DirectoryNotFoundException("Raiz do repositório não encontrada.");
    }
}
