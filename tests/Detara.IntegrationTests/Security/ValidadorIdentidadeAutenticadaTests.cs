using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Autenticacao;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Security;

public sealed class ValidadorIdentidadeAutenticadaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Empresa _empresa = null!;
    private Perfil _perfil = null!;
    private Permissao _permissao = null!;
    private Usuario _usuario = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlite(_connection)
            .Options;

        _empresa = new Empresa(
            "Empresa Segura",
            "Empresa Segura Ltda",
            "12345678000190",
            "empresa-segura");
        await using (var sistema = new DetaraDbContext(_options, ContextoUsuario.Anonimo))
        {
            await sistema.Database.EnsureCreatedAsync();
            sistema.Empresas.Add(_empresa);
            await sistema.SaveChangesAsync();
        }

        _perfil = new Perfil(_empresa.Id, "Administrador");
        _permissao = new Permissao("Clientes.Visualizar", "Visualizar clientes");
        _perfil.ConcederPermissao(_permissao);
        _usuario = new Usuario(
            _empresa.Id,
            _perfil.Id,
            "Usuário Seguro",
            "seguro@detara.local",
            "hash");
        await using var tenant = CriarContexto();
        tenant.AddRange(_permissao, _perfil, _usuario);
        await tenant.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task EstadoAtualCompativel_AceitaToken()
    {
        await using var context = CriarContexto();
        var validador = new ValidadorIdentidadeAutenticada(context);

        Assert.True(await validador.EhValidaAsync(CriarIdentidade(), CancellationToken.None));
    }

    [Fact]
    public async Task UsuarioDesativado_RevogaToken()
    {
        await AlterarAsync(context => context.Usuarios.SingleAsync(), usuario => usuario.Desativar());

        await AssertTokenRevogadoAsync();
    }

    [Fact]
    public async Task SenhaAlterada_RevogaToken()
    {
        await AlterarAsync(
            context => context.Usuarios.SingleAsync(),
            usuario => usuario.AlterarSenhaHash("novo-hash"));

        await AssertTokenRevogadoAsync();
    }

    [Fact]
    public async Task EmpresaDesativada_RevogaToken()
    {
        await using (var context = CriarContexto())
        {
            var empresa = await context.Empresas.SingleAsync(item => item.Id == _empresa.Id);
            empresa.Desativar();
            await context.SaveChangesAsync();
        }

        await AssertTokenRevogadoAsync();
    }

    [Fact]
    public async Task PerfilDesativado_RevogaToken()
    {
        await AlterarAsync(context => context.Perfis.SingleAsync(), perfil => perfil.Desativar());

        await AssertTokenRevogadoAsync();
    }

    [Fact]
    public async Task PermissaoRevogada_RevogaToken()
    {
        await using (var context = CriarContexto())
        {
            var permissao = await context.Permissoes.SingleAsync();
            permissao.Desativar();
            await context.SaveChangesAsync();
        }

        await AssertTokenRevogadoAsync();
    }

    [Fact]
    public async Task PermissaoForjadaNoToken_RevogaToken()
    {
        var identidade = CriarIdentidade() with
        {
            Permissoes = ["Clientes.Visualizar", "Financeiro.Visualizar"]
        };
        await using var context = CriarContexto();
        var validador = new ValidadorIdentidadeAutenticada(context);

        Assert.False(await validador.EhValidaAsync(identidade, CancellationToken.None));
    }

    [Fact]
    public async Task TenantDiferente_RevogaToken()
    {
        var identidade = CriarIdentidade() with { EmpresaId = Guid.NewGuid() };
        await using var context = CriarContexto();
        var validador = new ValidadorIdentidadeAutenticada(context);

        Assert.False(await validador.EhValidaAsync(identidade, CancellationToken.None));
    }

    private IdentidadeToken CriarIdentidade() => new(
        _usuario.Id,
        _empresa.Id,
        _perfil.Id,
        _usuario.VersaoSeguranca,
        _empresa.VersaoSeguranca,
        [_permissao.Codigo]);

    private async Task AssertTokenRevogadoAsync()
    {
        await using var context = CriarContexto();
        var validador = new ValidadorIdentidadeAutenticada(context);
        Assert.False(await validador.EhValidaAsync(CriarIdentidade(), CancellationToken.None));
    }

    private async Task AlterarAsync<T>(
        Func<DetaraDbContext, Task<T>> carregar,
        Action<T> alterar)
    {
        await using var context = CriarContexto();
        var entidade = await carregar(context);
        alterar(entidade);
        await context.SaveChangesAsync();
    }

    private DetaraDbContext CriarContexto() =>
        new(_options, new ContextoUsuario(_empresa.Id));

    private sealed class ContextoUsuario(Guid empresaId, bool autenticado = true) : IUsuarioContexto
    {
        public static ContextoUsuario Anonimo { get; } = new(Guid.Empty, false);
        public Guid UsuarioId { get; } = autenticado ? Guid.NewGuid() : Guid.Empty;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = autenticado;
    }
}
