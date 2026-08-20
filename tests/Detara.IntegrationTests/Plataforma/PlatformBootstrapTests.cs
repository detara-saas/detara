using Detara.Application.Abstracoes;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Plataforma;

public sealed class PlatformBootstrapTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using var db = CriarContexto();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Bootstrap_CriaSomentePrimeiroAdministradorEAudita()
    {
        var servico = CriarServico();
        var id = await servico.CriarPrimeiroAdministradorAsync(
            "Admin Inicial",
            "admin@detara.local",
            "passphrase-segura-inicial");

        await using var db = CriarContexto();
        var administrador = await db.AdministradoresPlataforma.SingleAsync();
        Assert.Equal(id, administrador.Id);
        Assert.False(administrador.MfaHabilitado);
        Assert.NotEqual("passphrase-segura-inicial", administrador.SenhaHash);
        Assert.Contains(
            await db.AuditoriasPlataforma.ToListAsync(),
            item => item.TipoAcao == AcoesAuditoriaPlataforma.PlatformAdminBootstrapCriado);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            servico.CriarPrimeiroAdministradorAsync(
                "Outro Admin",
                "outro@detara.local",
                "outra-passphrase-segura"));
        Assert.Contains("somente o primeiro", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ResetSenhaEMfa_IncrementaVersaoRemoveCodigosEAudita()
    {
        var servico = CriarServico();
        var id = await servico.CriarPrimeiroAdministradorAsync(
            "Admin Inicial",
            "admin@detara.local",
            "passphrase-segura-inicial");
        long versaoAnterior;
        await using (var db = CriarContexto())
        {
            var administrador = await db.AdministradoresPlataforma.SingleAsync();
            administrador.DefinirSegredoTotpProtegido("segredo-protegido");
            administrador.AtivarMfa(10);
            db.CodigosRecuperacaoAdministradoresPlataforma.Add(
                new CodigoRecuperacaoAdministradorPlataforma(id, "hash-codigo"));
            await db.SaveChangesAsync();
            versaoAnterior = administrador.VersaoSeguranca;
        }

        await servico.ResetarSenhaAsync("admin@detara.local", "nova-passphrase-segura");
        await servico.ResetarMfaAsync("admin@detara.local");

        await using var verificacao = CriarContexto();
        var atualizado = await verificacao.AdministradoresPlataforma.SingleAsync();
        Assert.True(atualizado.VersaoSeguranca >= versaoAnterior + 2);
        Assert.False(atualizado.MfaHabilitado);
        Assert.Empty(await verificacao.CodigosRecuperacaoAdministradoresPlataforma.ToListAsync());
        Assert.Contains(
            await verificacao.AuditoriasPlataforma.ToListAsync(),
            item => item.TipoAcao == AcoesAuditoriaPlataforma.PlatformAdminSenhaResetada);
        Assert.Contains(
            await verificacao.AuditoriasPlataforma.ToListAsync(),
            item => item.TipoAcao == AcoesAuditoriaPlataforma.PlatformAdminMfaResetado);
    }

    private PlatformBootstrapService CriarServico() => new(
        _options,
        new PasswordHasher<AdministradorPlataforma>());

    private DetaraDbContext CriarContexto() => new(_options, ContextoAnonimo.Instancia);

    private sealed class ContextoAnonimo : IUsuarioContexto
    {
        public static ContextoAnonimo Instancia { get; } = new();
        public Guid UsuarioId => Guid.Empty;
        public Guid EmpresaId => Guid.Empty;
        public bool EstaAutenticado => false;
    }
}
