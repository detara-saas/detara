using Detara.Application;
using Detara.Application.Abstracoes;
using Detara.Application.Autenticacao;
using Detara.Domain.Entidades;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.IntegrationTests.Autenticacao;

public sealed class AutenticarCommandTests
{
    [Fact]
    public async Task UsuarioInativo_NaoAutentica()
    {
        var usuario = CriarUsuario();
        usuario.Desativar();
        using var provider = CriarServicos(usuario);

        await Assert.ThrowsAsync<CredenciaisInvalidasException>(() => AutenticarAsync(provider));
    }

    [Fact]
    public async Task PerfilInativo_NaoAutentica()
    {
        var usuario = CriarUsuario();
        usuario.Perfil.Desativar();
        using var provider = CriarServicos(usuario);

        await Assert.ThrowsAsync<CredenciaisInvalidasException>(() => AutenticarAsync(provider));
    }

    [Fact]
    public async Task PermissaoInativa_NaoEhRetornada()
    {
        var usuario = CriarUsuario();
        var permissao = new Permissao("Clientes.Visualizar", "Visualizar clientes");
        permissao.Desativar();
        usuario.Perfil.ConcederPermissao(permissao);
        using var provider = CriarServicos(usuario);

        var resultado = await AutenticarAsync(provider);

        Assert.Empty(resultado.Permissoes);
    }

    [Fact]
    public async Task UsuarioInexistente_ExecutaVerificacaoFicticia()
    {
        var senha = new SenhaRastreavel();
        using var provider = CriarServicos(null, senha);

        await Assert.ThrowsAsync<CredenciaisInvalidasException>(() => AutenticarAsync(provider));

        Assert.True(senha.VerificacaoFicticiaExecutada);
    }

    private static ServiceProvider CriarServicos(
        Usuario? usuario,
        ISenhaServico? senhaServico = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AdicionarApplication();
        services.AddSingleton<IUsuarioAutenticacaoRepositorio>(new RepositorioFixo(usuario));
        services.AddSingleton(senhaServico ?? new SenhaSempreValida());
        services.AddSingleton<ITokenServico, TokenFixo>();
        return services.BuildServiceProvider();
    }

    private static Task<ResultadoAutenticacao> AutenticarAsync(IServiceProvider provider) =>
        provider.GetRequiredService<ISender>().Send(
            new AutenticarCommand("empresa-demo", "admin@detara.local", "senha-valida"));

    private static Usuario CriarUsuario()
    {
        var empresaId = Guid.NewGuid();
        var perfil = new Perfil(empresaId, "Administrador");
        var usuario = new Usuario(
            empresaId,
            perfil.Id,
            "Administrador Demo",
            "admin@detara.local",
            "hash");
        typeof(Usuario).GetProperty(nameof(Usuario.Perfil))!.SetValue(usuario, perfil);
        return usuario;
    }

    private sealed class RepositorioFixo(Usuario? usuario) : IUsuarioAutenticacaoRepositorio
    {
        public Task<Usuario?> ObterParaLoginAsync(
            string slugEmpresa,
            string email,
            CancellationToken cancellationToken) => Task.FromResult<Usuario?>(usuario);
    }

    private sealed class SenhaSempreValida : ISenhaServico
    {
        public string GerarHash(Usuario usuario, string senha) => "hash";
        public bool Verificar(Usuario usuario, string senhaHash, string senha) => true;
        public void VerificarContraHashFicticio(string senha) { }
    }

    private sealed class SenhaRastreavel : ISenhaServico
    {
        public bool VerificacaoFicticiaExecutada { get; private set; }
        public string GerarHash(Usuario usuario, string senha) => "hash";
        public bool Verificar(Usuario usuario, string senhaHash, string senha) => false;
        public void VerificarContraHashFicticio(string senha) => VerificacaoFicticiaExecutada = true;
    }

    private sealed class TokenFixo : ITokenServico
    {
        public TokenGerado Gerar(Usuario usuario) =>
            new("token", DateTime.UtcNow.AddMinutes(5));
    }
}
