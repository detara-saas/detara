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

    [Fact]
    public void PreferenciaUsuario_IniciaComPadroesSeguros()
    {
        var preferencia = new UsuarioPreferencia(Guid.NewGuid(), Guid.NewGuid());

        Assert.Equal("Sistema", preferencia.Tema);
        Assert.Equal("pt-BR", preferencia.Idioma);
        Assert.Equal("dashboard", preferencia.PaginaInicial);
        Assert.False(preferencia.SidebarRecolhida);
    }

    [Fact]
    public void Favorito_ExigePaginaConhecidaPeloChamador()
    {
        Assert.Throws<ArgumentException>(() =>
            new UsuarioPaginaFavorita(Guid.NewGuid(), Guid.NewGuid(), " ", 0));
    }

    [Fact]
    public void Cliente_NormalizaDocumentoEContatos()
    {
        var cliente = new Cliente(
            Guid.NewGuid(),
            "  João da Silva  ",
            TipoPessoa.PessoaFisica,
            "529.982.247-25",
            "(41) 99999-9999",
            "(41) 98888-7777",
            "  JOAO@EXEMPLO.COM ",
            new DateOnly(1990, 5, 20),
            null);

        Assert.Equal("João da Silva", cliente.Nome);
        Assert.Equal("52998224725", cliente.CpfCnpj);
        Assert.Equal("41999999999", cliente.Telefone);
        Assert.Equal("joao@exemplo.com", cliente.Email);
    }

    [Fact]
    public void Cliente_PermiteCadastroSemDocumento()
    {
        var cliente = new Cliente(
            Guid.NewGuid(),
            "Cliente rápido",
            TipoPessoa.PessoaFisica,
            null,
            "41999999999",
            null,
            null,
            null,
            null);

        Assert.Null(cliente.CpfCnpj);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Cliente_ExigeNome(string nome)
    {
        Assert.Throws<ArgumentException>(() => new Cliente(
            Guid.NewGuid(), nome, TipoPessoa.PessoaFisica, null, null, null, null, null, null));
    }

    [Fact]
    public void Cliente_RejeitaCpfInvalido()
    {
        Assert.Throws<ArgumentException>(() => new Cliente(
            Guid.NewGuid(),
            "Cliente",
            TipoPessoa.PessoaFisica,
            "111.111.111-11",
            null,
            null,
            null,
            null,
            null));
    }

    [Theory]
    [InlineData("ABC-1234", "ABC1234")]
    [InlineData("abc1d23", "ABC1D23")]
    public void Veiculo_NormalizaPlaca(string entrada, string esperado)
    {
        var veiculo = CriarVeiculo(entrada, 1000);

        Assert.Equal(esperado, veiculo.Placa);
    }

    [Fact]
    public void Veiculo_RejeitaPlacaInvalida()
    {
        Assert.Throws<ArgumentException>(() => CriarVeiculo("AB-123", 1000));
    }

    [Fact]
    public void Veiculo_RejeitaQuilometragemNegativa()
    {
        Assert.Throws<ArgumentException>(() => CriarVeiculo("ABC1D23", -1));
    }

    private static Veiculo CriarVeiculo(string placa, int quilometragem) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        placa,
        "Honda",
        "Civic",
        "Touring",
        2024,
        2024,
        "Preto",
        quilometragem,
        null);
}
