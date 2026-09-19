using Detara.Application.Abstracoes;
using Detara.Domain.Capacidades;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Capacidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Capacidades;

public sealed class EmpresaCapacidadesPersistenciaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private readonly Guid _empresaA = Guid.NewGuid();
    private readonly Guid _empresaB = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        await using var sistema = new DetaraDbContext(_options, Contexto.Anonymous);
        await sistema.Database.EnsureCreatedAsync();
        var a = EmpresaComId(_empresaA, "Empresa A", "empresa-a", "11111111000111");
        var b = EmpresaComId(_empresaB, "Empresa B", "empresa-b", "22222222000122");
        sistema.Empresas.AddRange(a, b);
        await sistema.SaveChangesAsync();

        await using var tenantA = new DetaraDbContext(_options, new Contexto(_empresaA));
        tenantA.EmpresasCapacidades.AddRange(PresetCapacidadesEmpresa.Criar(_empresaA, a.SegmentoCodigo));
        await tenantA.SaveChangesAsync();
        await using var tenantB = new DetaraDbContext(_options, new Contexto(_empresaB));
        tenantB.EmpresasCapacidades.AddRange(PresetCapacidadesEmpresa.Criar(_empresaB, b.SegmentoCodigo));
        var veiculosB = tenantB.EmpresasCapacidades.Local.Single(item =>
            item.Codigo == CodigosCapacidadeEmpresa.Veiculos);
        var checkInB = tenantB.EmpresasCapacidades.Local.Single(item =>
            item.Codigo == CodigosCapacidadeEmpresa.CheckIn);
        checkInB.Alterar(false, checkInB.Versao);
        veiculosB.Alterar(false, veiculosB.Versao);
        await tenantB.SaveChangesAsync();
    }

    [Fact]
    public async Task SnapshotScoped_ReutilizaInstanciaEIsolaTenant()
    {
        var contexto = new Contexto(_empresaA);
        await using var db = new DetaraDbContext(_options, contexto);
        var servico = new EmpresaCapacidadesServico(db, contexto);

        var primeiro = await servico.ObterSnapshotAsync();
        var segundo = await servico.ObterSnapshotAsync();

        Assert.Same(primeiro, segundo);
        Assert.True(primeiro.Possui(CodigosCapacidadeEmpresa.Veiculos));
        Assert.Equal(CatalogoCapacidadesEmpresa.Todas.Count, primeiro.Capacidades.Count);
    }

    [Fact]
    public async Task TenantA_NaoPodeEscreverCapacidadeDoTenantB()
    {
        await using var db = new DetaraDbContext(_options, new Contexto(_empresaA));
        db.EmpresasCapacidades.Add(new EmpresaCapacidade(
            _empresaB,
            CodigosCapacidadeEmpresa.Veiculos,
            true));

        await Assert.ThrowsAsync<ViolacaoIsolamentoTenantException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EmpresaCodigo_PossuiUnicidadeNoBanco()
    {
        await using var db = new DetaraDbContext(_options, new Contexto(_empresaA));
        db.EmpresasCapacidades.Add(new EmpresaCapacidade(
            _empresaA,
            CodigosCapacidadeEmpresa.Veiculos,
            true));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    private static Empresa EmpresaComId(Guid id, string nome, string slug, string documento)
    {
        var empresa = new Empresa(nome, $"{nome} Ltda", documento, slug);
        typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(empresa, id);
        return empresa;
    }

    private sealed class Contexto(Guid empresaId, bool autenticado = true) : IUsuarioContexto
    {
        public static Contexto Anonymous { get; } = new(Guid.Empty, false);
        public Guid UsuarioId { get; } = autenticado ? Guid.NewGuid() : Guid.Empty;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = autenticado;
    }
}
