using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;

namespace Detara.UnitTests.Domain;

public sealed class AdministracaoTenantTests
{
    [Fact]
    public void PerfilSistema_NaoPodeSerAtualizadoOuInativado()
    {
        var perfil = new Perfil(Guid.NewGuid(), "Administrador", "Acesso total", true);

        Assert.Throws<InvalidOperationException>(() =>
            perfil.Atualizar("Outro", null, [], perfil.Versao));
        Assert.Throws<InvalidOperationException>(() =>
            perfil.AlterarStatus(false, perfil.Versao));
    }

    [Fact]
    public void Perfil_NormalizaNomeEControlaConcorrencia()
    {
        var perfil = new Perfil(Guid.NewGuid(), "  Atendimento  ");

        Assert.Equal("Atendimento", perfil.Nome);
        Assert.Equal("ATENDIMENTO", perfil.NomeNormalizado);
        Assert.Throws<InvalidOperationException>(() =>
            perfil.Atualizar("Recepção", null, [], perfil.Versao + 1));
    }

    [Fact]
    public void PerfilCustomizado_AtualizaPermissoesEIncrementaVersao()
    {
        var perfil = new Perfil(Guid.NewGuid(), "Atendimento");
        var permissao = new Permissao("Clientes.Visualizar", "Visualizar clientes");

        perfil.Atualizar("Recepção", "Acesso de recepção", [permissao], perfil.Versao);

        Assert.Equal(2, perfil.Versao);
        Assert.Equal("RECEPÇÃO", perfil.NomeNormalizado);
        Assert.Contains(permissao, perfil.Permissoes);
    }

    [Fact]
    public void Usuario_AlterarNomeNaoRevogaSessao()
    {
        var usuario = CriarUsuario();
        var versaoSeguranca = usuario.VersaoSeguranca;

        usuario.AlterarNome("Nome atualizado", usuario.Versao);

        Assert.Equal(versaoSeguranca, usuario.VersaoSeguranca);
        Assert.Equal(2, usuario.Versao);
    }

    [Fact]
    public void Usuario_AlterarEmailNormalizaERevogaSessao()
    {
        var usuario = CriarUsuario();

        usuario.AlterarEmail("  NOVO@EXEMPLO.COM  ", usuario.Versao);

        Assert.Equal("novo@exemplo.com", usuario.Email);
        Assert.Equal(2, usuario.VersaoSeguranca);
    }

    [Fact]
    public void Usuario_AlterarPerfilRevogaSessao()
    {
        var usuario = CriarUsuario();

        usuario.AlterarPerfil(Guid.NewGuid(), usuario.Versao);

        Assert.Equal(2, usuario.VersaoSeguranca);
    }

    [Fact]
    public void Usuario_InativarEReativarRevogamSessoes()
    {
        var usuario = CriarUsuario();

        usuario.DesativarAcesso(usuario.Versao);
        usuario.ReativarAcesso(usuario.Versao);

        Assert.True(usuario.EhAtivo);
        Assert.Equal(3, usuario.VersaoSeguranca);
    }

    [Fact]
    public void Empresa_AtualizacaoCadastralNaoAlteraVersaoDeSeguranca()
    {
        var empresa = new Empresa("Empresa", "Empresa Ltda", "12345678000190", "empresa");

        empresa.AtualizarCadastro("Novo nome", "Empresa Ltda", "12345678000190",
            "contato@empresa.com", null, "America/Sao_Paulo", empresa.VersaoCadastro);

        Assert.Equal(1, empresa.VersaoSeguranca);
        Assert.Equal(2, empresa.VersaoCadastro);
        Assert.Equal("contato@empresa.com", empresa.Email);
    }

    [Fact]
    public void ConviteTenant_ReenvioInvalidaTokenAnteriorEPreservaOrigem()
    {
        var convite = ConviteAdministradorEmpresa.CriarParaUsuarioTenant(
            Guid.NewGuid(), Guid.NewGuid(), "usuario@empresa.test", Guid.NewGuid());
        var agora = DateTime.UtcNow;
        convite.IniciarEnvio("hash-antigo", agora.AddHours(24), agora);
        convite.RegistrarEnvio("provider", agora);

        convite.PrepararReenvioTenant(agora.AddMinutes(1), Guid.NewGuid());

        Assert.Equal(OrigemConviteAcessoEmpresa.UsuarioTenant, convite.Origem);
        Assert.Equal(StatusConviteAdministradorEmpresa.Pendente, convite.Status);
        Assert.False(convite.PodeSerAceito("hash-antigo", agora.AddMinutes(2)));
    }

    private static Usuario CriarUsuario() => new(
        Guid.NewGuid(), Guid.NewGuid(), "Usuário", "usuario@exemplo.com", "hash");
}
