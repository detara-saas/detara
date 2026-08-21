using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http.Json;
using Detara.Application.Abstracoes;
using Detara.Application.Plataforma;
using Detara.Contracts.Comum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Detara.IntegrationTests.Security;

[Collection("api-security")]
public sealed class JwtEEndpointsSecurityTests : IAsyncLifetime
{
    private const string Emissor = "Detara.Security.Tests";
    private const string Audiencia = "Detara.Security.Tests.Web";
    private static readonly string Chave = Convert.ToBase64String(
        SHA512.HashData(Encoding.UTF8.GetBytes("detara-security-tests-signing-key")));
    private static readonly string ChavePlataforma = Convert.ToBase64String(
        SHA512.HashData(Encoding.UTF8.GetBytes("detara-platform-security-tests-signing-key")));
    private readonly ApiSecurityFactory _factory = new();
    private HttpClient _client = null!;

    public Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task JwtHs256Valido_AutenticaMasRespeitaPermissao()
    {
        UsarToken(CriarToken(SecurityAlgorithms.HmacSha256));

        var response = await _client.GetAsync("/api/clientes");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData(SecurityAlgorithms.HmacSha512)]
    [InlineData(SecurityAlgorithms.HmacSha384)]
    public async Task JwtComAlgoritmoNaoPermitido_EhRejeitado(string algoritmo)
    {
        UsarToken(CriarToken(algoritmo));

        var response = await _client.GetAsync("/api/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task JwtExpirado_EhRejeitado()
    {
        UsarToken(CriarToken(
            SecurityAlgorithms.HmacSha256,
            DateTime.UtcNow.AddMinutes(-10),
            DateTime.UtcNow.AddMinutes(-5)));

        var response = await _client.GetAsync("/api/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task JwtAdulterado_EhRejeitado()
    {
        var token = CriarToken(SecurityAlgorithms.HmacSha256);
        var partes = token.Split('.');
        partes[1] = partes[1][..^1] + (partes[1][^1] == 'a' ? 'b' : 'a');
        UsarToken(string.Join('.', partes));

        var response = await _client.GetAsync("/api/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenTenant_NaoAutenticaEndpointPlataforma()
    {
        UsarToken(CriarToken(SecurityAlgorithms.HmacSha256));

        var response = await _client.GetAsync("/api/plataforma/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenPlataforma_NaoAutenticaEndpointTenant()
    {
        UsarToken(CriarTokenPlataforma(incluirMfa: true));

        var response = await _client.GetAsync("/api/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenPlataformaSemMfa_NaoCriaSessaoAdministrativa()
    {
        UsarToken(CriarTokenPlataforma(incluirMfa: false));

        var response = await _client.GetAsync("/api/plataforma/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenPlataformaComAudienciaTenant_EhRejeitado()
    {
        UsarToken(CriarTokenPlataforma(incluirMfa: true, audiencia: Audiencia));

        var response = await _client.GetAsync("/api/plataforma/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenPlataformaExpirado_EhRejeitado()
    {
        UsarToken(CriarTokenPlataforma(
            incluirMfa: true,
            validoAPartir: DateTime.UtcNow.AddMinutes(-10),
            expiraEm: DateTime.UtcNow.AddMinutes(-5)));

        var response = await _client.GetAsync("/api/plataforma/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TokenPlataformaComAssinaturaErrada_EhRejeitado()
    {
        UsarToken(CriarTokenPlataforma(
            incluirMfa: true,
            chave: Convert.ToBase64String(RandomNumberGenerator.GetBytes(64))));

        var response = await _client.GetAsync("/api/plataforma/dashboard");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(20)]
    [InlineData(int.MaxValue)]
    public async Task ListagemEmpresasPlataforma_TamanhoPaginaInvalidoRetornaErroControlado(
        int tamanhoPagina)
    {
        UsarToken(CriarTokenPlataforma(incluirMfa: true));

        using var response = await _client.GetAsync(
            $"/api/plataforma/empresas?tamanhoPagina={tamanhoPagina}");
        var conteudo = await response.Content.ReadAsStringAsync();
        var resposta = await response.Content.ReadFromJsonAsync<RespostaApi<object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.NotNull(resposta);
        Assert.False(resposta.Sucesso);
        Assert.Equal("validacao", resposta.Erro?.Codigo);
        Assert.Contains("TamanhoPagina", resposta.Erro?.Detalhes?.Keys ?? []);
        Assert.DoesNotContain("ValidationException", conteudo, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack trace", conteudo, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("/api/plataforma/autenticacao/login", 6)]
    [InlineData("/api/plataforma/autenticacao/mfa/verificar", 9)]
    [InlineData("/api/convites/administrador/validar", 11)]
    public async Task EndpointsSensiveisAplicamRateLimitPorOrigem(string rota, int quantidade)
    {
        HttpResponseMessage? ultima = null;
        for (var tentativa = 0; tentativa < quantidade; tentativa++)
        {
            ultima?.Dispose();
            ultima = await _client.PostAsJsonAsync(rota, new { });
        }

        using (ultima)
        {
            Assert.NotNull(ultima);
            Assert.Equal(HttpStatusCode.TooManyRequests, ultima.StatusCode);
        }
    }

    [Fact]
    public async Task EndpointAnonimo_FicaRestritoAWhitelistAuditada()
    {
        var dataSource = _factory.Services.GetRequiredService<EndpointDataSource>();
        var anonimos = dataSource.Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .SelectMany(endpoint =>
            {
                var rota = "/" + endpoint.RoutePattern.RawText?.TrimStart('/');
                var metodos = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["ANY"];
                return metodos.Select(metodo => $"{metodo} {rota}");
            })
            .OrderBy(item => item, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(
            [
                "GET /health",
                "POST /api/autenticacao/login",
                "POST /api/convites/administrador/aceitar",
                "POST /api/convites/administrador/validar",
                "POST /api/plataforma/autenticacao/login",
                "POST /api/plataforma/autenticacao/mfa/ativar",
                "POST /api/plataforma/autenticacao/mfa/configuracao",
                "POST /api/plataforma/autenticacao/mfa/verificar"
            ],
            anonimos);
        var autorizacao = _factory.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<AuthorizationOptions>>();
        Assert.NotNull(autorizacao.Value.FallbackPolicy);
    }

    [Fact]
    public void Bootstrap_NaoExisteNaSuperficieHttp()
    {
        var rotas = _factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Select(x => x.RoutePattern.RawText ?? string.Empty)
            .ToArray();

        Assert.DoesNotContain(rotas, rota => rota.Contains("bootstrap", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(rotas, rota => rota.Contains("superadmin", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(110, rotas.Length);
    }

    [Fact]
    public async Task RespostasDaApi_IncluemBaselineDeHeadersSeguros()
    {
        var response = await _client.GetAsync("/api/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Equal("DENY", response.Headers.GetValues("X-Frame-Options").Single());
        Assert.Equal("no-referrer", response.Headers.GetValues("Referrer-Policy").Single());
        Assert.Equal("no-store", response.Headers.CacheControl?.ToString());
        Assert.True(response.Headers.Contains("X-Trace-Id"));
        Assert.Contains("default-src 'none'", response.Headers.GetValues("Content-Security-Policy").Single());
    }

    [Fact]
    public async Task OrigemCorsNaoPermitida_NaoRecebeCabecalhoDeLiberacao()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/clientes");
        request.Headers.Add("Origin", "https://origem-maliciosa.example");

        var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    [Fact]
    public async Task ProductionComHostWildcard_FalhaNoStartup()
    {
        await using var factory = new ProductionFactory("*");

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("AllowedHosts", exception.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ProductionComConfiguracaoCriticaExplicita_Inicia()
    {
        await using var factory = new ProductionFactory("api.detara.example");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://api.detara.example")
        });

        var response = await client.GetAsync("/api/clientes");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private void UsarToken(string token)
    {
        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
    }

    private static string CriarToken(
        string algoritmo,
        DateTime? validoAPartir = null,
        DateTime? expiraEm = null)
    {
        var agora = DateTime.UtcNow;
        var claims = new Claim[]
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new("empresa_id", Guid.NewGuid().ToString()),
            new("perfil_id", Guid.NewGuid().ToString()),
            new("usuario_atualizado_ticks", "0"),
            new("empresa_versao_seguranca", "1")
        };
        var token = new JwtSecurityToken(
            Emissor,
            Audiencia,
            claims,
            validoAPartir ?? agora.AddMinutes(-1),
            expiraEm ?? agora.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Chave)),
                algoritmo));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CriarTokenPlataforma(
        bool incluirMfa,
        string? audiencia = null,
        string? chave = null,
        DateTime? validoAPartir = null,
        DateTime? expiraEm = null)
    {
        var agora = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, Guid.NewGuid().ToString()),
            new("identidade", "platform_admin"),
            new("versao_seguranca", "1")
        };
        if (incluirMfa)
        {
            claims.Add(new Claim("amr", "mfa"));
        }

        var token = new JwtSecurityToken(
            "Detara.Platform.Security.Tests",
            audiencia ?? "detara-platform-tests",
            claims,
            validoAPartir ?? agora.AddMinutes(-1),
            expiraEm ?? agora.AddMinutes(5),
            new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(chave ?? ChavePlataforma)),
                SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class ApiSecurityFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly Dictionary<string, string?> _ambienteAnterior = new();

        public ApiSecurityFactory()
        {
            DefinirAmbiente("Jwt__Emissor", Emissor);
            DefinirAmbiente("Jwt__Audiencia", Audiencia);
            DefinirAmbiente("Jwt__ChaveAssinatura", Chave);
            DefinirAmbiente("Jwt__ExpiracaoMinutos", "60");
            DefinirAmbiente("PlatformJwt__Emissor", "Detara.Platform.Security.Tests");
            DefinirAmbiente("PlatformJwt__Audiencia", "detara-platform-tests");
            DefinirAmbiente("PlatformJwt__ChaveAssinatura", ChavePlataforma);
            DefinirAmbiente("PlatformJwt__ExpiracaoMinutos", "45");
            DefinirAmbiente("ConnectionStrings__DefaultConnection", "Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IValidadorIdentidadeAutenticada>();
                services.AddSingleton<IValidadorIdentidadeAutenticada, ValidadorSempreAtivo>();
                services.RemoveAll<IAutenticacaoPlataformaServico>();
                services.AddSingleton<IAutenticacaoPlataformaServico, ValidadorPlataformaSempreAtivo>();
            });
        }

        public new async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            foreach (var (chave, valor) in _ambienteAnterior)
            {
                Environment.SetEnvironmentVariable(chave, valor);
            }
        }

        private void DefinirAmbiente(string chave, string valor)
        {
            _ambienteAnterior[chave] = Environment.GetEnvironmentVariable(chave);
            Environment.SetEnvironmentVariable(chave, valor);
        }
    }

    private sealed class ValidadorSempreAtivo : IValidadorIdentidadeAutenticada
    {
        public Task<bool> EhValidaAsync(
            IdentidadeToken identidade,
            CancellationToken cancellationToken) => Task.FromResult(true);
    }

    private sealed class ValidadorPlataformaSempreAtivo : IAutenticacaoPlataformaServico
    {
        public Task<bool> RevalidarAsync(Guid administradorPlataformaId, long versaoSeguranca, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<InicioAutenticacaoPlataformaResultado> IniciarAsync(string email, string senha, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ConfiguracaoMfaPlataformaResultado> ObterConfiguracaoMfaAsync(string desafio, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutenticacaoMfaPlataformaResultado> AtivarMfaAsync(string desafio, string codigo, string? traceId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<AutenticacaoMfaPlataformaResultado> VerificarMfaAsync(string desafio, string codigo, string? traceId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyCollection<string>> RegenerarCodigosRecuperacaoAsync(Guid administradorPlataformaId, string senhaAtual, string codigoTotp, string? traceId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class ProductionFactory : WebApplicationFactory<Program>, IAsyncDisposable
    {
        private readonly Dictionary<string, string?> _ambienteAnterior = new();

        public ProductionFactory(string allowedHosts)
        {
            DefinirAmbiente("AllowedHosts", allowedHosts);
            DefinirAmbiente("Cors__OrigensPermitidas__0", "https://app.detara.example");
            DefinirAmbiente("Jwt__Emissor", Emissor);
            DefinirAmbiente("Jwt__Audiencia", Audiencia);
            DefinirAmbiente("Jwt__ChaveAssinatura", Chave);
            DefinirAmbiente("Jwt__ExpiracaoMinutos", "60");
            DefinirAmbiente("PlatformJwt__Emissor", "Detara.Platform.Security.Tests");
            DefinirAmbiente("PlatformJwt__Audiencia", "detara-platform-tests");
            DefinirAmbiente("PlatformJwt__ChaveAssinatura", Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)));
            DefinirAmbiente("PlatformJwt__ExpiracaoMinutos", "45");
            DefinirAmbiente("Web__PublicBaseUrl", "https://app.detara.example");
            DefinirAmbiente("DataProtection__KeyRingPath", Path.GetFullPath("data/test-dp-keys"));
            DefinirAmbiente("ConnectionStrings__DefaultConnection", "Server=localhost;Database=unused;Integrated Security=true;TrustServerCertificate=true");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureLogging(logging => logging.ClearProviders());
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IValidadorIdentidadeAutenticada>();
                services.AddSingleton<IValidadorIdentidadeAutenticada, ValidadorSempreAtivo>();
            });
        }

        public new async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            foreach (var (chave, valor) in _ambienteAnterior)
            {
                Environment.SetEnvironmentVariable(chave, valor);
            }
        }

        private void DefinirAmbiente(string chave, string valor)
        {
            _ambienteAnterior[chave] = Environment.GetEnvironmentVariable(chave);
            Environment.SetEnvironmentVariable(chave, valor);
        }
    }
}

[CollectionDefinition("api-security", DisableParallelization = true)]
public sealed class ApiSecurityCollection;
