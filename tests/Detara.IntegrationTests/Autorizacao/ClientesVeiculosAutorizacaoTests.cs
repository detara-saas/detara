using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Detara.Application.Abstracoes;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Clientes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Autorizacao;

public sealed class ClientesVeiculosAutorizacaoTests : IAsyncLifetime
{
    private readonly DetaraApiFactory _factory = new();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        await _factory.InicializarBancoAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task UsuarioSemClientesVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComClientesVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.ClientesVisualizar);
        var response = await _client.GetAsync("/api/clientes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemClientesEditar_AlteracaoBloqueada()
    {
        UsarPermissoes(Permissoes.ClientesVisualizar);
        var request = new SalvarClienteRequest(
            "Cliente alterado",
            "PessoaFisica",
            "52998224725",
            null,
            null,
            null,
            null,
            null);
        var response = await _client.PutAsJsonAsync($"/api/clientes/{_factory.ClienteId}", request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemVeiculosVisualizar_Recebe403()
    {
        UsarPermissoes(Permissoes.ClientesVisualizar);
        var response = await _client.GetAsync("/api/veiculos");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private void UsarPermissoes(params string[] permissoes)
    {
        _client.DefaultRequestHeaders.Remove(TestAuthHandler.PermissionsHeader);
        _client.DefaultRequestHeaders.Add(TestAuthHandler.PermissionsHeader, string.Join(',', permissoes));
    }

    private sealed class DetaraApiFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly Dictionary<string, string?> _environmentBeforeTest = new();
        public Guid EmpresaId { get; } = Guid.NewGuid();
        public Guid ClienteId { get; private set; }

        public DetaraApiFactory()
        {
            SetTestEnvironment("Jwt__Emissor", "Detara.Tests");
            SetTestEnvironment("Jwt__Audiencia", "Detara.Tests");
            SetTestEnvironment("Jwt__ChaveAssinatura", Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)));
            SetTestEnvironment("Jwt__ExpiracaoMinutos", "60");
            SetTestEnvironment("ConnectionStrings__DefaultConnection", "Data Source=unused");
            _connection.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<DetaraDbContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<DetaraDbContext>>();
                services.RemoveAll<DetaraDbContext>();
                services.AddDbContext<DetaraDbContext>(options => options.UseSqlite(_connection));
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = TestAuthHandler.SchemeName;
                    options.DefaultChallengeScheme = TestAuthHandler.SchemeName;
                    options.DefaultForbidScheme = TestAuthHandler.SchemeName;
                }).AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });
                services.AddSingleton(new TestTenant(EmpresaId));
            });
        }

        public async Task InicializarBancoAsync()
        {
            using var scope = Services.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
            await using var systemContext = new DetaraDbContext(options, TestUserContext.Anonymous);
            await systemContext.Database.EnsureCreatedAsync();
            var empresa = new Empresa("Empresa Teste", "Empresa Teste Ltda", "12345678000190", "empresa-teste");
            typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(empresa, EmpresaId);
            systemContext.Empresas.Add(empresa);
            await systemContext.SaveChangesAsync();

            await using var tenantContext = new DetaraDbContext(options, new TestUserContext(EmpresaId));
            var cliente = new Cliente(
                EmpresaId,
                "Cliente Teste",
                TipoPessoa.PessoaFisica,
                "52998224725",
                null,
                null,
                null,
                null,
                null);
            tenantContext.Clientes.Add(cliente);
            await tenantContext.SaveChangesAsync();
            ClienteId = cliente.Id;
        }

        public new async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
            foreach (var (key, value) in _environmentBeforeTest)
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }

        private void SetTestEnvironment(string key, string value)
        {
            _environmentBeforeTest[key] = Environment.GetEnvironmentVariable(key);
            Environment.SetEnvironmentVariable(key, value);
        }
    }

    private sealed record TestTenant(Guid EmpresaId);

    private sealed class TestAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        TestTenant tenant)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        public const string SchemeName = "Test";
        public const string PermissionsHeader = "X-Test-Permissions";

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var claims = new List<Claim>
            {
                new("sub", Guid.NewGuid().ToString()),
                new("empresa_id", tenant.EmpresaId.ToString()),
                new("name", "Usuário Teste")
            };
            var permissions = Request.Headers[PermissionsHeader].ToString()
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            claims.AddRange(permissions.Select(permission => new Claim("permissao", permission)));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName, "name", "role"));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class TestUserContext(Guid empresaId, bool autenticado = true) : IUsuarioContexto
    {
        public static TestUserContext Anonymous { get; } = new(Guid.Empty, false);
        public Guid UsuarioId { get; } = autenticado ? Guid.NewGuid() : Guid.Empty;
        public Guid EmpresaId { get; } = empresaId;
        public bool EstaAutenticado { get; } = autenticado;
    }
}
