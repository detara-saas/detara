using Detara.Domain.Entidades;

namespace Detara.UnitTests.Domain;

public sealed class EntidadesTests
{
    [Fact]
    public void Empresa_NormalizaSlug()
    {
        var empresa = new Empresa(
            "Premium Detail",
            "Premium Detail Ltda",
            "12345678000190",
            "  PREMIUM-DETAIL  ");

        Assert.Equal("premium-detail", empresa.Slug);
        Assert.True(empresa.EhAtivo);
        Assert.NotEqual(Guid.Empty, empresa.Id);
    }

    [Theory]
    [InlineData("slug com espaco")]
    [InlineData("-slug")]
    [InlineData("slug-")]
    [InlineData("slug_com_underscore")]
    public void Empresa_RejeitaSlugInvalido(string slug)
    {
        Assert.Throws<ArgumentException>(() => new Empresa(
            "Premium Detail",
            "Premium Detail Ltda",
            "12345678000190",
            slug));
    }

    [Fact]
    public void Perfil_ExigeEmpresaValida()
    {
        var exception = Assert.Throws<ArgumentException>(() => new Perfil(Guid.Empty, "Administrador"));

        Assert.Equal("empresaId", exception.ParamName);
    }

    [Fact]
    public void Usuario_NormalizaEmail()
    {
        var usuario = new Usuario(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Ana Silva",
            "  ANA@EXEMPLO.COM  ",
            "hash-seguro");

        Assert.Equal("ana@exemplo.com", usuario.Email);
    }
}
