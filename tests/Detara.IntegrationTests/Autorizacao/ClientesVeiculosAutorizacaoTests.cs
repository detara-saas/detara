using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Detara.Application.Abstracoes;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Clientes;
using Detara.Contracts.Comum;
using Detara.Domain.Entidades;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
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

    [Fact]
    public async Task UsuarioSemConfiguracoesVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/configuracoes/operacao");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComConfiguracoesVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.ConfiguracoesVisualizar);
        var response = await _client.GetAsync("/api/configuracoes/operacao");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/configuracoes/operacao")]
    [InlineData("/api/configuracoes/operacao/checklist")]
    public async Task UsuarioSemConfiguracoesEditar_AlteracaoBloqueada(string rota)
    {
        UsarPermissoes(Permissoes.ConfiguracoesVisualizar);
        var response = await _client.PutAsJsonAsync(rota, new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/veiculos/00000000-0000-0000-0000-000000000001/fotos")]
    [InlineData("/api/veiculos/00000000-0000-0000-0000-000000000001/fotos/00000000-0000-0000-0000-000000000002/conteudo")]
    public async Task UsuarioSemVeiculosVisualizar_FotosBloqueadas(string rota)
    {
        var response = await _client.GetAsync(rota);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("PATCH", "/api/veiculos/00000000-0000-0000-0000-000000000001/fotos/00000000-0000-0000-0000-000000000002/principal")]
    [InlineData("DELETE", "/api/veiculos/00000000-0000-0000-0000-000000000001/fotos/00000000-0000-0000-0000-000000000002")]
    public async Task UsuarioSemVeiculosEditar_MutacoesDeFotoBloqueadas(string metodo, string rota)
    {
        UsarPermissoes(Permissoes.VeiculosVisualizar);
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota);
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemVeiculosEditar_UploadDeFotoBloqueado()
    {
        UsarPermissoes(Permissoes.VeiculosVisualizar);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xD9]), "arquivo", "foto.jpg");
        var response = await _client.PostAsync(
            "/api/veiculos/00000000-0000-0000-0000-000000000001/fotos",
            content);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/servicos")]
    [InlineData("/api/categorias-servico")]
    public async Task UsuarioSemServicosVisualizar_Recebe403(string rota)
    {
        var response = await _client.GetAsync(rota);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComServicosVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.ServicosVisualizar);
        var response = await _client.GetAsync("/api/servicos");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/servicos", Permissoes.ServicosCriar)]
    [InlineData("/api/pacotes", Permissoes.PacotesCriar)]
    public async Task UsuarioSemPermissaoDeCriacao_Recebe403(string rota, string permissaoNecessaria)
    {
        UsarPermissoes(Permissoes.ServicosVisualizar, Permissoes.PacotesVisualizar);
        Assert.DoesNotContain(permissaoNecessaria, _client.DefaultRequestHeaders.GetValues(TestAuthHandler.PermissionsHeader).Single());
        var response = await _client.PostAsJsonAsync(rota, new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("/api/servicos/00000000-0000-0000-0000-000000000001", Permissoes.ServicosEditar)]
    [InlineData("/api/pacotes/00000000-0000-0000-0000-000000000001", Permissoes.PacotesEditar)]
    public async Task UsuarioSemPermissaoDeEdicao_Recebe403(string rota, string permissaoNecessaria)
    {
        UsarPermissoes(Permissoes.ServicosVisualizar, Permissoes.PacotesVisualizar);
        Assert.DoesNotContain(permissaoNecessaria, _client.DefaultRequestHeaders.GetValues(TestAuthHandler.PermissionsHeader).Single());
        var response = await _client.PutAsJsonAsync(rota, new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemPacotesVisualizar_Recebe403()
    {
        UsarPermissoes(Permissoes.ServicosVisualizar);
        var response = await _client.GetAsync("/api/pacotes");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComPacotesVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.PacotesVisualizar);
        var response = await _client.GetAsync("/api/pacotes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemAgendaVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/agenda/contexto");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemAgendaCriar_PostBloqueado()
    {
        UsarPermissoes(Permissoes.AgendaVisualizar);
        var response = await _client.PostAsJsonAsync("/api/agendamentos", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("PUT", "/api/agendamentos/00000000-0000-0000-0000-000000000001")]
    [InlineData("PATCH", "/api/agendamentos/00000000-0000-0000-0000-000000000001/status")]
    [InlineData("PATCH", "/api/agendamentos/00000000-0000-0000-0000-000000000001/reagendar")]
    public async Task UsuarioSemAgendaEditar_AlteracoesBloqueadas(string metodo, string rota)
    {
        UsarPermissoes(Permissoes.AgendaVisualizar, Permissoes.AgendaCriar);
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota) { Content = JsonContent.Create(new { }) };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemOrcamentosVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/orcamentos");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemOrcamentosCriar_PostBloqueado()
    {
        UsarPermissoes(Permissoes.OrcamentosVisualizar);
        var response = await _client.PostAsJsonAsync("/api/orcamentos", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task OrcamentoComItemValido_PassaPeloPipelineESalvaRascunho()
    {
        UsarPermissoes(Permissoes.OrcamentosCriar);
        var request = new SalvarOrcamentoRequest(
            _factory.ClienteId,
            _factory.VeiculoId,
            null,
            DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7),
            null,
            null,
            "À vista",
            0,
            0,
            [new(TipoItemOrcamentoContrato.Servico, _factory.ServicoId, null, null, 160m, 1, null)]);

        var response = await _client.PostAsJsonAsync("/api/orcamentos", request);
        var conteudo = await response.Content.ReadFromJsonAsync<RespostaApi<OrcamentoDetalheResponse>>();

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.True(conteudo?.Sucesso);
        Assert.Equal(160m, conteudo?.Resultado?.Total);
        Assert.Single(conteudo!.Resultado!.Itens);
    }

    [Fact]
    public async Task OrcamentoInvalido_RetornaDetalhesNoFormatoPadraoDaApi()
    {
        UsarPermissoes(Permissoes.OrcamentosCriar);
        var request = new SalvarOrcamentoRequest(
            Guid.Empty,
            Guid.Empty,
            null,
            default,
            null,
            null,
            null,
            -1,
            0,
            [new(TipoItemOrcamentoContrato.Servico, null, null, null, -1, 0, null)]);

        var response = await _client.PostAsJsonAsync("/api/orcamentos", request);
        var conteudo = await response.Content.ReadFromJsonAsync<RespostaApi<object>>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(conteudo?.Sucesso);
        Assert.Equal("validacao", conteudo?.Erro?.Codigo);
        Assert.Contains("ClienteId", conteudo!.Erro!.Detalhes!.Keys);
        Assert.Contains("Itens[0].ValorUnitario", conteudo.Erro.Detalhes.Keys);
        Assert.Contains("Itens[0].Quantidade", conteudo.Erro.Detalhes.Keys);
    }

    [Fact]
    public async Task PdfOrcamento_Autorizado_RetornaArquivoPdfValido()
    {
        UsarPermissoes(Permissoes.OrcamentosVisualizar);
        var response = await _client.GetAsync($"/api/orcamentos/{_factory.OrcamentoId}/pdf");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 1000);
        Assert.Equal("%PDF-", System.Text.Encoding.ASCII.GetString(bytes, 0, 5));
    }

    [Theory]
    [InlineData("PUT", "/api/orcamentos/00000000-0000-0000-0000-000000000001")]
    [InlineData("POST", "/api/orcamentos/00000000-0000-0000-0000-000000000001/emitir")]
    [InlineData("POST", "/api/orcamentos/00000000-0000-0000-0000-000000000001/aprovar")]
    [InlineData("POST", "/api/orcamentos/00000000-0000-0000-0000-000000000001/recusar")]
    [InlineData("POST", "/api/orcamentos/00000000-0000-0000-0000-000000000001/cancelar")]
    public async Task UsuarioSemOrcamentosEditar_AlteracoesBloqueadas(string metodo, string rota)
    {
        UsarPermissoes(Permissoes.OrcamentosVisualizar, Permissoes.OrcamentosCriar);
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota) { Content = JsonContent.Create(new { observacao = (string?)null }) };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemOrdemServicoVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/ordens-servico");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComOrdemServicoVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.OrdemServicoVisualizar);
        var response = await _client.GetAsync("/api/ordens-servico");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemOrdemServicoCriar_PostBloqueado()
    {
        UsarPermissoes(Permissoes.OrdemServicoVisualizar);
        var response = await _client.PostAsJsonAsync("/api/ordens-servico", new { });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/check-in", Permissoes.OrdemServicoEditar)]
    [InlineData("PUT", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/checklist", Permissoes.OrdemServicoEditar)]
    [InlineData("POST", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/iniciar-execucao", Permissoes.OrdemServicoFinalizar)]
    [InlineData("POST", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/finalizar-execucao", Permissoes.OrdemServicoFinalizar)]
    [InlineData("POST", "/api/ordens-servico/00000000-0000-0000-0000-000000000001/concluir", Permissoes.OrdemServicoFinalizar)]
    public async Task UsuarioSemPermissaoOperacionalDaOs_Recebe403(string metodo, string rota, string permissao)
    {
        UsarPermissoes(Permissoes.OrdemServicoVisualizar);
        Assert.DoesNotContain(permissao,
            _client.DefaultRequestHeaders.GetValues(TestAuthHandler.PermissionsHeader).Single());
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota)
        {
            Content = JsonContent.Create(new { observacao = (string?)null })
        };
        var response = await _client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioSemFinanceiroVisualizar_Recebe403()
    {
        var response = await _client.GetAsync("/api/financeiro/resumo");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UsuarioComFinanceiroVisualizar_ConsultaPermitida()
    {
        UsarPermissoes(Permissoes.FinanceiroVisualizar);
        var response = await _client.GetAsync("/api/financeiro/resumo");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("POST", "/api/financeiro/contas-receber/00000000-0000-0000-0000-000000000001/pagamentos", Permissoes.FinanceiroRegistrarPagamento)]
    [InlineData("POST", "/api/financeiro/contas-receber/00000000-0000-0000-0000-000000000001/pagamentos/00000000-0000-0000-0000-000000000002/estornar", Permissoes.FinanceiroEstornarPagamento)]
    [InlineData("PATCH", "/api/financeiro/contas-receber/00000000-0000-0000-0000-000000000001/vencimento", Permissoes.FinanceiroEditar)]
    public async Task UsuarioSemPermissaoFinanceiraDeMutacao_Recebe403(string metodo, string rota, string permissao)
    {
        UsarPermissoes(Permissoes.FinanceiroVisualizar);
        Assert.DoesNotContain(permissao,
            _client.DefaultRequestHeaders.GetValues(TestAuthHandler.PermissionsHeader).Single());
        using var request = new HttpRequestMessage(new HttpMethod(metodo), rota)
        { Content = JsonContent.Create(new { }) };
        var response = await _client.SendAsync(request);
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
        public Guid VeiculoId { get; private set; }
        public Guid ServicoId { get; private set; }
        public Guid OrcamentoId { get; private set; }

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
            var veiculo = new Veiculo(EmpresaId, cliente.Id, "ABC1D23", "Honda", "Civic", null,
                2024, 2024, "Preto", 15000, null);
            tenantContext.Veiculos.Add(veiculo);
            var categoria = new CategoriaServico(EmpresaId, "Lavagem", null, 1);
            tenantContext.CategoriasServico.Add(categoria);
            await tenantContext.SaveChangesAsync();
            var servico = new Servico(EmpresaId, categoria.Id, "Lavagem técnica", null,
                TipoPrecificacao.APartirDe, 100m, 90, 1);
            tenantContext.Servicos.Add(servico);
            await tenantContext.SaveChangesAsync();
            VeiculoId = veiculo.Id;
            ServicoId = servico.Id;
            var usuarioId = Guid.NewGuid();
            var orcamento = new Orcamento(EmpresaId,
                new(cliente.Id, cliente.Nome, cliente.CpfCnpj, cliente.Telefone, veiculo.Id, "Honda Civic", "ABC1D23"),
                null, null, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7), "Proposta comercial", null, "À vista", 0, 0,
                [new(TipoItemOrcamento.Servico, Guid.NewGuid(), "Lavagem técnica", null, TipoPrecificacao.APartirDe, 100m, 160m, 1, 1, null)], usuarioId);
            orcamento.Emitir(DateTime.UtcNow.Year, usuarioId);
            tenantContext.Orcamentos.Add(orcamento);
            await tenantContext.SaveChangesAsync();
            OrcamentoId = orcamento.Id;
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
