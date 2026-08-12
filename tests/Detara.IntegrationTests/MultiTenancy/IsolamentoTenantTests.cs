using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.MultiTenancy;

public sealed class IsolamentoTenantTests : IAsyncLifetime
{
    private readonly Guid _empresaAId = Guid.NewGuid();
    private readonly Guid _empresaBId = Guid.NewGuid();
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private Guid _perfilAId;
    private Guid _perfilBId;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;

        await using var contextA = CriarContexto(_empresaAId);
        await contextA.Database.EnsureCreatedAsync();
        contextA.Empresas.AddRange(
            new Empresa("Empresa A", "Empresa A Ltda", "11111111000111", "empresa-a"),
            new Empresa("Empresa B", "Empresa B Ltda", "22222222000122", "empresa-b"));
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

    private DetaraDbContext CriarContexto(Guid empresaId) =>
        new(_options, new UsuarioContextoTeste(empresaId));

    private sealed class UsuarioContextoTeste(Guid empresaId) : IUsuarioContexto
    {
        public Guid UsuarioId { get; } = Guid.NewGuid();
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado => true;
    }
}
