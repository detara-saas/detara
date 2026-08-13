using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Autenticacao;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.MultiTenancy;

public sealed class IsolamentoTenantTests : IAsyncLifetime
{
    private Guid _empresaAId;
    private Guid _empresaBId;
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _perfilAId;
    private Guid _perfilBId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;

        var empresaA = new Empresa("Empresa A", "Empresa A Ltda", "11111111000111", "empresa-a");
        var empresaB = new Empresa("Empresa B", "Empresa B Ltda", "22222222000122", "empresa-b");
        _empresaAId = empresaA.Id;
        _empresaBId = empresaB.Id;

        await using var contextA = CriarContexto(_empresaAId);
        await contextA.Database.EnsureCreatedAsync();
        contextA.Empresas.AddRange(empresaA, empresaB);
        var perfilA = new Perfil(_empresaAId, "Administrador A");
        contextA.Perfis.Add(perfilA);
        await contextA.SaveChangesAsync();
        _perfilAId = perfilA.Id;

        await using var contextB = CriarContexto(_empresaBId);
        var perfilB = new Perfil(_empresaBId, "Administrador B");
        contextB.Perfis.Add(perfilB);
        await contextB.SaveChangesAsync();
        _perfilBId = perfilB.Id;
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task UsuarioEmpresaA_NaoPodeConsultarRegistroEmpresaB()
    {
        await using var contextA = CriarContexto(_empresaAId);

        var perfilEmpresaB = await contextA.Perfis.SingleOrDefaultAsync(x => x.Id == _perfilBId);

        Assert.Null(perfilEmpresaB);
    }

    [Fact]
    public async Task UsuarioEmpresaA_NaoPodeEditarRegistroEmpresaB()
    {
        await using var contextA = CriarContexto(_empresaAId);
        var perfilEmpresaB = await contextA.Perfis.IgnoreQueryFilters().SingleAsync(x => x.Id == _perfilBId);
        perfilEmpresaB.AlterarNome("Tentativa indevida");

        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => contextA.SaveChangesAsync());
    }

    [Fact]
    public async Task UsuarioEmpresaA_NaoPodeExcluirRegistroEmpresaB()
    {
        await using var contextA = CriarContexto(_empresaAId);
        var perfilEmpresaB = await contextA.Perfis.IgnoreQueryFilters().SingleAsync(x => x.Id == _perfilBId);
        contextA.Perfis.Remove(perfilEmpresaB);

        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => contextA.SaveChangesAsync());
    }

    [Fact]
    public async Task ConsultaEmpresaA_RetornaSomenteRegistrosEmpresaA()
    {
        await using var contextA = CriarContexto(_empresaAId);

        var perfis = await contextA.Perfis.ToListAsync();

        var perfil = Assert.Single(perfis);
        Assert.Equal(_perfilAId, perfil.Id);
        Assert.Equal(_empresaAId, perfil.EmpresaId);
    }

    [Fact]
    public async Task UsuarioEmpresaA_NaoPodeCriarRegistroParaEmpresaB()
    {
        await using var contextA = CriarContexto(_empresaAId);
        contextA.Perfis.Add(new Perfil(_empresaBId, "Perfil indevido"));

        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => contextA.SaveChangesAsync());
    }

    [Fact]
    public async Task UsuarioSemTenant_NaoPodeConsultarDadosTenant()
    {
        await using var context = new DetaraDbContext(_options, UsuarioContextoTeste.Anonimo);

        Assert.Empty(await context.Perfis.ToListAsync());
        Assert.Empty(await context.Usuarios.ToListAsync());
    }

    [Fact]
    public async Task AlterarEmpresaId_DeRegistroCarregadoEhBloqueado()
    {
        await using var contextA = CriarContexto(_empresaAId);
        var perfilA = await contextA.Perfis.SingleAsync(x => x.Id == _perfilAId);

        Assert.Throws<InvalidOperationException>(() =>
            contextA.Entry(perfilA).Property(x => x.EmpresaId).CurrentValue = _empresaBId);
    }

    [Fact]
    public async Task UpdateDesconectadoComTenantForjado_NaoEditaOutraEmpresa()
    {
        await using var contextA = CriarContexto(_empresaAId);
        var perfilForjado = new Perfil(_empresaAId, "Tentativa desconectada");
        contextA.Entry(perfilForjado).Property(x => x.Id).CurrentValue = _perfilBId;
        contextA.Perfis.Update(perfilForjado);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextA.SaveChangesAsync());

        await using var contextB = CriarContexto(_empresaBId);
        var perfilB = await contextB.Perfis.SingleAsync(x => x.Id == _perfilBId);
        Assert.Equal("Administrador B", perfilB.Nome);
    }

    [Fact]
    public async Task DeleteDesconectadoComTenantForjado_NaoExcluiOutraEmpresa()
    {
        await using var contextA = CriarContexto(_empresaAId);
        var perfilForjado = new Perfil(_empresaAId, "Tentativa desconectada");
        contextA.Entry(perfilForjado).Property(x => x.Id).CurrentValue = _perfilBId;
        contextA.Perfis.Remove(perfilForjado);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => contextA.SaveChangesAsync());

        await using var contextB = CriarContexto(_empresaBId);
        Assert.True(await contextB.Perfis.AnyAsync(x => x.Id == _perfilBId));
    }

    [Fact]
    public void TodaEntidadeTenant_PossuiFiltroEConcorrenciaPorEmpresa()
    {
        using var contextA = CriarContexto(_empresaAId);
        var tiposTenant = contextA.Model.GetEntityTypes()
            .Where(tipo => typeof(EntidadeEmpresaBase).IsAssignableFrom(tipo.ClrType));

        Assert.All(tiposTenant, tipo =>
        {
            Assert.NotEmpty(tipo.GetDeclaredQueryFilters());
            Assert.True(tipo.FindProperty(nameof(EntidadeEmpresaBase.EmpresaId))!.IsConcurrencyToken);
        });
    }

    [Fact]
    public async Task EmpresaInativa_NaoEhResolvidaParaLogin()
    {
        await using var contextB = CriarContexto(_empresaBId);
        var empresaB = await contextB.Empresas.SingleAsync(x => x.Id == _empresaBId);
        empresaB.Desativar();
        await contextB.SaveChangesAsync();
        var repositorio = new UsuarioAutenticacaoRepositorio(contextB);

        var usuario = await repositorio.ObterParaLoginAsync(
            "empresa-b",
            "qualquer@detara.local",
            CancellationToken.None);

        Assert.Null(usuario);
    }

    private DetaraDbContext CriarContexto(Guid empresaId) =>
        new(_options, new UsuarioContextoTeste(empresaId));

    private sealed class UsuarioContextoTeste(Guid empresaId, bool estaAutenticado = true) : IUsuarioContexto
    {
        public static UsuarioContextoTeste Anonimo { get; } = new(Guid.Empty, false);
        public Guid UsuarioId { get; } = estaAutenticado ? Guid.NewGuid() : Guid.Empty;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = estaAutenticado;
    }
}
