using System.Text.RegularExpressions;
using Detara.Application.Abstracoes;
using Detara.Application.Comunicacao;
using Detara.Application.Plataforma;
using Detara.Domain.Entidades;
using Detara.Domain.Plataforma;
using Detara.Infrastructure.Persistencia;
using Detara.Infrastructure.Plataforma;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Plataforma;

public sealed class FilaConvitesPlataformaTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private DbContextOptions<DetaraDbContext> _options = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _options = new DbContextOptionsBuilder<DetaraDbContext>().UseSqlite(_connection).Options;
        await using var db = CriarContexto();
        await db.Database.EnsureCreatedAsync();
        var admin = new AdministradorPlataforma("Admin", "admin@detara.local", "hash");
        admin.DefinirSegredoTotpProtegido("protegido");
        admin.AtivarMfa(1);
        db.AdministradoresPlataforma.Add(admin);
        await db.SaveChangesAsync();
        var servico = new AdministracaoPlataformaServico(db, _options, new PasswordHasher<Usuario>());
        await servico.ProvisionarEmpresaAsync(
            admin.Id,
            new ProvisionarEmpresaEntrada(
                "Empresa <Segura>",
                "Empresa Segura Ltda",
                "12345678000190",
                null,
                null,
                "America/Sao_Paulo",
                "Admin <Tenant>",
                "tenant@empresa.local"),
            "trace",
            CancellationToken.None);
    }

    public async Task DisposeAsync() => await _connection.DisposeAsync();

    [Fact]
    public async Task EnvioComSucesso_GeraTokenForaDaTransacaoPersisteApenasHashEEscapaHtml()
    {
        var provedor = new ProvedorFalso(new ResultadoEnvioEmail(true, false, "provider-id", null));
        using var provider = CriarProvider(provedor);
        var fila = CriarFila(provider);

        Assert.Equal(1, await fila.ProcessarLoteAsync(CancellationToken.None));

        await using var db = CriarContexto();
        var convite = await db.ConvitesAdministradoresEmpresa.SingleAsync();
        Assert.Equal(StatusConviteAdministradorEmpresa.Enviado, convite.Status);
        Assert.NotNull(convite.TokenHash);
        Assert.NotNull(convite.ExpiraEmUtc);
        Assert.Equal(1, convite.QuantidadeTentativasEnvio);
        Assert.NotNull(provedor.Mensagem);
        Assert.Contains("/ativar-conta#token=", provedor.Mensagem.CorpoHtml, StringComparison.Ordinal);
        Assert.Contains("Empresa &lt;Segura&gt;", provedor.Mensagem.CorpoHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Admin <Tenant>", provedor.Mensagem.CorpoHtml, StringComparison.Ordinal);

        var token = Regex.Match(provedor.Mensagem.CorpoHtml, "#token=([^&\"]+)").Groups[1].Value;
        Assert.True(token.Length >= 43);
        Assert.NotEqual(token, convite.TokenHash);
        Assert.Equal(ConvitesAdministradoresEmpresaServico.HashToken(token), convite.TokenHash);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FalhaDoProvedor_NaoDesfazTenantENaoDeixaConviteProcessando(bool lancarExcecao)
    {
        var provedor = new ProvedorFalso(
            new ResultadoEnvioEmail(false, true, null, "provedor indisponível"),
            lancarExcecao);
        using var provider = CriarProvider(provedor);
        var fila = CriarFila(provider);

        Assert.Equal(1, await fila.ProcessarLoteAsync(CancellationToken.None));

        await using var db = CriarContexto();
        Assert.Equal(1, await db.Empresas.CountAsync());
        Assert.Equal(1, await db.Usuarios.IgnoreQueryFilters().CountAsync());
        var convite = await db.ConvitesAdministradoresEmpresa.SingleAsync();
        Assert.Equal(StatusConviteAdministradorEmpresa.Pendente, convite.Status);
        Assert.Equal(1, convite.QuantidadeTentativasEnvio);
        Assert.NotNull(convite.ProximaTentativaEnvioEmUtc);
        Assert.NotNull(convite.TokenHash);
        Assert.DoesNotContain("token", convite.UltimoErroSeguro ?? string.Empty, StringComparison.OrdinalIgnoreCase);
    }

    private ServiceProvider CriarProvider(IProvedorEmail provedor)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => new DetaraDbContext(_options, ContextoAnonimo.Instancia));
        services.AddSingleton(provedor);
        return services.BuildServiceProvider();
    }

    private static IFilaConvitesAdministradoresEmpresaServico CriarFila(IServiceProvider provider) =>
        new FilaConvitesAdministradoresEmpresaServico(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new PlataformaOptions
            {
                ConvitesTamanhoLote = 10,
                ConviteExpiracaoHoras = 72,
                ConvitesMaximoTentativas = 4
            }),
            Options.Create(new WebPublicaOptions { PublicBaseUrl = "https://app.detara.test" }),
            NullLogger<FilaConvitesAdministradoresEmpresaServico>.Instance);

    private DetaraDbContext CriarContexto() => new(_options, ContextoAnonimo.Instancia);

    private sealed class ProvedorFalso(ResultadoEnvioEmail resultado, bool lancarExcecao = false) : IProvedorEmail
    {
        public MensagemEmailProvedor? Mensagem { get; private set; }
        public Task<ResultadoEnvioEmail> EnviarAsync(MensagemEmailProvedor mensagem, CancellationToken cancellationToken)
        {
            Mensagem = mensagem;
            return lancarExcecao
                ? throw new HttpRequestException("detalhe sensível simulado")
                : Task.FromResult(resultado);
        }
    }

    private sealed class ContextoAnonimo : IUsuarioContexto
    {
        public static ContextoAnonimo Instancia { get; } = new();
        public Guid UsuarioId => Guid.Empty;
        public Guid EmpresaId => Guid.Empty;
        public bool EstaAutenticado => false;
    }
}
