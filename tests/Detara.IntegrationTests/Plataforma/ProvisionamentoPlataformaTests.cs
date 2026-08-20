using Detara.Application.Abstracoes;
using Detara.Application.Plataforma;
using Detara.Contracts.Autorizacao;
using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Detara.IntegrationTests.Plataforma;

public sealed class ProvisionamentoPlataformaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;
    private AdministradorPlataforma _administrador = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>()
            .UseSqlite(_connection)
            .Options;
        await using var db = CriarContexto();
        await db.Database.EnsureCreatedAsync();
        _administrador = new AdministradorPlataforma("Admin", "admin@detara.local", "hash");
        _administrador.DefinirSegredoTotpProtegido("protegido");
        _administrador.AtivarMfa(1);
        db.AdministradoresPlataforma.Add(_administrador);
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task Provisionamento_CriaTenantCompletoAtomicamenteSemTokenOuSenhaConhecida()
    {
        await using var db = CriarContexto();
        var servico = CriarServico(db);

        var resultado = await servico.ProvisionarEmpresaAsync(
            _administrador.Id,
            EntradaPadrao(),
            "trace-provisionamento",
            CancellationToken.None);

        await using var verificacao = CriarContexto();
        var empresa = await verificacao.Empresas.SingleAsync();
        var perfil = await verificacao.Perfis.IgnoreQueryFilters()
            .Include(x => x.Permissoes)
            .SingleAsync();
        var usuario = await verificacao.Usuarios.IgnoreQueryFilters().SingleAsync();
        var convite = await verificacao.ConvitesAdministradoresEmpresa.SingleAsync();

        Assert.Equal(resultado.Id, empresa.Id);
        Assert.Equal("oficina-acme", empresa.Slug);
        Assert.Equal(Permissoes.Definicoes.Count, perfil.Permissoes.Count);
        Assert.Equal(
            Permissoes.Todas.OrderBy(x => x),
            perfil.Permissoes.Select(x => x.Codigo).OrderBy(x => x));
        Assert.False(usuario.EhAtivo);
        Assert.NotEqual("pendente", usuario.SenhaHash);
        Assert.NotEqual("qualquer-senha-conhecida", usuario.SenhaHash);
        Assert.Equal(StatusConviteAdministradorEmpresa.Pendente, convite.Status);
        Assert.Null(convite.TokenHash);
        Assert.Null(convite.ExpiraEmUtc);
        Assert.Contains(
            await verificacao.AuditoriasPlataforma.ToListAsync(),
            item => item.TipoAcao == AcoesAuditoriaPlataforma.EmpresaProvisionada &&
                item.TraceId == "trace-provisionamento");
    }

    [Fact]
    public async Task DocumentoDuplicado_NaoCriaSegundoGrafoParcial()
    {
        await using var db = CriarContexto();
        var servico = CriarServico(db);
        await servico.ProvisionarEmpresaAsync(
            _administrador.Id,
            EntradaPadrao(),
            "trace-1",
            CancellationToken.None);

        await Assert.ThrowsAsync<ConflitoRegraNegocioException>(() =>
            servico.ProvisionarEmpresaAsync(
                _administrador.Id,
                EntradaPadrao() with { NomeFantasia = "Outra empresa" },
                "trace-2",
                CancellationToken.None));

        await using var verificacao = CriarContexto();
        Assert.Equal(1, await verificacao.Empresas.CountAsync());
        Assert.Equal(1, await verificacao.Usuarios.IgnoreQueryFilters().CountAsync());
        Assert.Equal(1, await verificacao.ConvitesAdministradoresEmpresa.CountAsync());
        Assert.Equal(1, await verificacao.AuditoriasPlataforma.CountAsync(
            x => x.TipoAcao == AcoesAuditoriaPlataforma.EmpresaProvisionada));
    }

    [Fact]
    public async Task SuspenderEReativar_RevogaTokenTenantPorVersaoEAuditaMotivo()
    {
        await using var db = CriarContexto();
        var servico = CriarServico(db);
        var provisionada = await servico.ProvisionarEmpresaAsync(
            _administrador.Id,
            EntradaPadrao(),
            "trace",
            CancellationToken.None);
        var versaoInicial = (await db.Empresas.SingleAsync(x => x.Id == provisionada.Id)).VersaoSeguranca;

        await servico.SuspenderEmpresaAsync(
            _administrador.Id,
            provisionada.Id,
            "inadimplência contratual",
            "trace-suspensao",
            CancellationToken.None);
        db.ChangeTracker.Clear();
        var suspensa = await db.Empresas.SingleAsync(x => x.Id == provisionada.Id);
        Assert.False(suspensa.EhAtivo);
        Assert.True(suspensa.VersaoSeguranca > versaoInicial);
        var versaoSuspensao = suspensa.VersaoSeguranca;

        db.ChangeTracker.Clear();
        await servico.ReativarEmpresaAsync(
            _administrador.Id,
            provisionada.Id,
            "contrato regularizado",
            "trace-reativacao",
            CancellationToken.None);
        db.ChangeTracker.Clear();
        var reativada = await db.Empresas.SingleAsync(x => x.Id == provisionada.Id);
        Assert.True(reativada.EhAtivo);
        Assert.True(reativada.VersaoSeguranca > versaoSuspensao);
        Assert.Contains(
            await db.AuditoriasPlataforma.ToListAsync(),
            x => x.TipoAcao == AcoesAuditoriaPlataforma.EmpresaSuspensa &&
                x.DescricaoSegura == "inadimplência contratual");
        Assert.Contains(
            await db.AuditoriasPlataforma.ToListAsync(),
            x => x.TipoAcao == AcoesAuditoriaPlataforma.EmpresaReativada &&
                x.DescricaoSegura == "contrato regularizado");
    }

    [Fact]
    public async Task AuditoriaPlataforma_EAppendOnly()
    {
        await using var db = CriarContexto();
        var auditoria = new AuditoriaPlataforma(
            _administrador.Id,
            AcoesAuditoriaPlataforma.LoginRealizado,
            null,
            _administrador.Id,
            "trace",
            "login");
        db.AuditoriasPlataforma.Add(auditoria);
        await db.SaveChangesAsync();

        db.AuditoriasPlataforma.Remove(auditoria);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.Contains("append-only", erro.Message, StringComparison.OrdinalIgnoreCase);
    }

    private IAdministracaoPlataformaServico CriarServico(DetaraDbContext db) =>
        new AdministracaoPlataformaServico(
            db,
            _options,
            new PasswordHasher<Usuario>());

    private static ProvisionarEmpresaEntrada EntradaPadrao() => new(
        "Oficina Ácme",
        "Oficina Ácme Ltda",
        "12.345.678/0001-90",
        "contato@acme.local",
        "41999999999",
        "America/Sao_Paulo",
        "Administradora Acme",
        "admin@acme.local");

    private DetaraDbContext CriarContexto() => new(_options, ContextoAnonimo.Instancia);

    private sealed class ContextoAnonimo : IUsuarioContexto
    {
        public static ContextoAnonimo Instancia { get; } = new();
        public Guid UsuarioId => Guid.Empty;
        public Guid EmpresaId => Guid.Empty;
        public bool EstaAutenticado => false;
    }
}
