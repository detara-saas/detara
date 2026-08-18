using Detara.Domain.Atendimento;
using Detara.Domain.Clientes;

namespace Detara.UnitTests.Domain;

public sealed class FundacaoOperacionalTests
{
    [Fact]
    public void ConfiguracaoOperacional_ArmazenaNiveisDistintos()
    {
        var configuracao = new ConfiguracaoOperacionalAtendimento(
            Guid.NewGuid(),
            NivelExigenciaOperacional.Obrigatorio,
            NivelExigenciaOperacional.Opcional,
            NivelExigenciaOperacional.Desabilitado);

        Assert.Equal(NivelExigenciaOperacional.Obrigatorio, configuracao.ChecklistEntrada);
        Assert.Equal(NivelExigenciaOperacional.Opcional, configuracao.FotosEntrada);
        Assert.Equal(NivelExigenciaOperacional.Desabilitado, configuracao.FotosSaida);
    }

    [Fact]
    public void ConfiguracaoOperacional_RejeitaNivelInvalido()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new ConfiguracaoOperacionalAtendimento(
                Guid.NewGuid(),
                (NivelExigenciaOperacional)99,
                NivelExigenciaOperacional.Desabilitado,
                NivelExigenciaOperacional.Desabilitado));
    }

    [Fact]
    public void Checklist_NormalizaItensEOrdenaExplicitamente()
    {
        var checklist = new ChecklistModelo(
            Guid.NewGuid(),
            "  Entrada premium  ",
            "  Avaliação inicial  ",
            ["  Riscos aparentes  ", "Rodas danificadas"]);

        Assert.Equal("Entrada premium", checklist.Nome);
        Assert.Equal("Avaliação inicial", checklist.Descricao);
        Assert.Equal([1, 2], checklist.Itens.Select(item => item.Ordem));
        Assert.Equal(["Riscos aparentes", "Rodas danificadas"], checklist.Itens.Select(item => item.Descricao));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Checklist_RejeitaItemVazio(string descricao)
    {
        Assert.Throws<ArgumentException>(() =>
            new ChecklistModelo(Guid.NewGuid(), ChecklistModelo.NomePadrao, null, [descricao]));
    }

    [Fact]
    public void Checklist_RejeitaDuplicidadeSemDiferenciarMaiusculas()
    {
        Assert.Throws<ArgumentException>(() =>
            new ChecklistModelo(
                Guid.NewGuid(),
                ChecklistModelo.NomePadrao,
                null,
                ["Riscos aparentes", "  riscos aparentes  "]));
    }

    [Fact]
    public void VeiculoFoto_PreservaSomenteMetadados()
    {
        var foto = CriarFoto(true);

        Assert.StartsWith("empresas/", foto.ChaveStorage);
        Assert.Equal("fachada.jpg", foto.NomeOriginal);
        Assert.Equal("image/jpeg", foto.ContentType);
        Assert.Equal(4096, foto.TamanhoBytes);
        Assert.True(foto.EhPrincipal);
    }

    [Fact]
    public void VeiculoFoto_PermiteTrocarEstadoPrincipal()
    {
        var foto = CriarFoto(false);

        foto.DefinirComoPrincipal(true);

        Assert.True(foto.EhPrincipal);
        Assert.NotNull(foto.AtualizadoEmUtc);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("image/svg+xml")]
    [InlineData("application/pdf")]
    public void VeiculoFoto_RejeitaContentTypeNaoSuportado(string contentType)
    {
        Assert.Throws<ArgumentException>(() => new VeiculoFoto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "empresas/a/veiculos/b/fotos/c.jpg",
            "foto.jpg",
            contentType,
            100,
            false,
            Guid.NewGuid()));
    }

    private static VeiculoFoto CriarFoto(bool principal) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        $"empresas/{Guid.NewGuid():N}/veiculos/{Guid.NewGuid():N}/fotos/{Guid.NewGuid():N}.jpg",
        "fachada.jpg",
        "image/jpeg",
        4096,
        principal,
        Guid.NewGuid());
}
