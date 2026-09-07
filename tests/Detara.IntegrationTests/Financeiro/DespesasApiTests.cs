using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Detara.Application.Abstracoes;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Financeiro;
using Detara.Domain.Entidades;
using Detara.Domain.Financeiro;
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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Detara.IntegrationTests.Financeiro;

[Collection("api-security")]
public sealed class DespesasApiTests : IAsyncLifetime
{
    private readonly Factory _factory = new();
    private HttpClient _client = null!;
    private const string Base = "/api/financeiro/despesas";
    private static readonly DateOnly Mes = new(2026, 9, 1);
    public async Task InitializeAsync() { _client = _factory.CreateClient(); await _factory.PrepararAsync(); Autorizar(); }
    public async Task DisposeAsync() { _client.Dispose(); await _factory.DisposeAsync(); }
    private void Autorizar(params string[] permissoes)
    {
        _client.DefaultRequestHeaders.Remove("X-Permissions");
        _client.DefaultRequestHeaders.Add("X-Permissions", string.Join(',', permissoes.Length > 0 ? permissoes :
            [Permissoes.FinanceiroVisualizar, Permissoes.FinanceiroEditar, Permissoes.FinanceiroRegistrarPagamento, Permissoes.FinanceiroEstornarPagamento]));
        _client.DefaultRequestHeaders.Remove("X-Tenant"); _client.DefaultRequestHeaders.Add("X-Tenant", _factory.A.ToString());
    }
    public static IEnumerable<object[]> Rotas()
    {
        foreach (var r in new[] { ("GET", ""), ("GET", "/{conta}"), ("POST", ""), ("PUT", "/{conta}"),
            ("POST", "/{conta}/pagar"), ("POST", "/{conta}/estornar"), ("POST", "/{conta}/cancelar"),
            ("GET", "/recorrencias"), ("GET", "/recorrencias/{regra}"), ("POST", "/recorrencias"),
            ("PUT", "/recorrencias/{regra}"), ("POST", "/recorrencias/{regra}/atividade"),
            ("GET", "/categorias"), ("POST", "/categorias"), ("PUT", "/categorias/{categoria}") }) yield return [r.Item1, r.Item2];
    }
    private string Caminho(string rota) => Base + rota.Replace("{conta}", _factory.ContaB.ToString()).Replace("{regra}", _factory.RegraB.ToString()).Replace("{categoria}", _factory.CategoriaB.ToString());
    private object Dados => new
    {
        descricao = "Teste",
        nome = "Teste",
        categoriaId = _factory.CategoriaA,
        valor = 100m,
        competencia = Mes,
        vencimento = Mes,
        diaVencimento = 10,
        competenciaInicial = Mes,
        versao = 1,
        dataPagamento = Mes,
        valorPago = 100m,
        motivo = "Correção de lançamento",
        ativa = false
    };
    [Theory, MemberData(nameof(Rotas))]
    public async Task TodasRotas_Anonimo401_SemPermissao403(string metodo, string rota)
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant");
        using var anonimo = new HttpRequestMessage(new(metodo), Caminho(rota)) { Content = JsonContent.Create(Dados) };
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.SendAsync(anonimo)).StatusCode);
        Autorizar("sem-permissao");
        using var sem = new HttpRequestMessage(new(metodo), Caminho(rota)) { Content = JsonContent.Create(Dados) };
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(sem)).StatusCode);
    }
    public static IEnumerable<object[]> RotasEscrita() => Rotas().Where(x => (string)x[0] != "GET");
    public static IEnumerable<object[]> RotasComId() => Rotas().Where(x => ((string)x[1]).Contains('{'));
    [Theory, MemberData(nameof(RotasEscrita))]
    public async Task Visualizar_NaoAutorizaEscrita(string metodo, string rota)
    {
        Autorizar(Permissoes.FinanceiroVisualizar);
        using var request = new HttpRequestMessage(new(metodo), Caminho(rota)) { Content = JsonContent.Create(Dados) };
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.SendAsync(request)).StatusCode);
    }
    [Theory, MemberData(nameof(RotasComId))]
    public async Task IdsDeOutroTenant_NaoSaoLidosNemAlterados(string metodo, string rota)
    {
        using var request = new HttpRequestMessage(new(metodo), Caminho(rota)) { Content = JsonContent.Create(Dados) };
        Assert.Equal(HttpStatusCode.NotFound, (await _client.SendAsync(request)).StatusCode);
    }
    [Fact]
    public async Task Listagens_Isoladas_CategoriaInjetadaRejeitada_EEmpresaIdIgnorado()
    {
        var contas = await _client.GetFromJsonAsync<RespostaApi<DespesasResponse>>(Base + "?competencia=2026-09-01");
        Assert.DoesNotContain(contas!.Resultado!.Contas.Itens, x => x.Id == _factory.ContaB);
        var regras = await _client.GetFromJsonAsync<RespostaApi<PaginaResponse<RecorrenciaDespesaResponse>>>(Base + "/recorrencias");
        Assert.DoesNotContain(regras!.Resultado!.Itens, x => x.Id == _factory.RegraB);
        var cats = await _client.GetFromJsonAsync<RespostaApi<CategoriaDespesaResponse[]>>(Base + "/categorias");
        Assert.DoesNotContain(cats!.Resultado!, x => x.Id == _factory.CategoriaB);
        var r = await _client.PostAsJsonAsync(Base, new SalvarDespesaRequest("Ataque", _factory.CategoriaB, 100, Mes, Mes, null, null));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        r = await _client.PostAsJsonAsync(Base + "/recorrencias", new SalvarRecorrenciaRequest("Ataque", _factory.CategoriaB, 100, 10, Mes, null, null, null));
        Assert.Equal(HttpStatusCode.NotFound, r.StatusCode);
        r = await _client.PostAsJsonAsync(Base, new { descricao = "Tenant do token", categoriaId = _factory.CategoriaA, valor = 100, competencia = Mes, vencimento = Mes, empresaId = _factory.B, status = 2, valorPago = 100 });
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var id = (await r.Content.ReadFromJsonAsync<RespostaApi<DespesaIdResponse>>())!.Resultado!.Id;
        var criada = (await _client.GetFromJsonAsync<RespostaApi<DespesaDetalheResponse>>(Base + $"/{id}"))!.Resultado!.Conta;
        Assert.Equal(StatusDespesaContrato.Pendente, criada.Status); Assert.Null(criada.ValorPago);
        _client.DefaultRequestHeaders.Remove("X-Tenant"); _client.DefaultRequestHeaders.Add("X-Tenant", _factory.B.ToString());
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(Base + $"/{id}")).StatusCode);
    }
    [Fact]
    public async Task CriarEditarPagarEstornarCancelar_ErrosPadrao_EVersaoConcorrente()
    {
        var criada = await _client.PostAsJsonAsync(Base, new SalvarDespesaRequest("Despesa", _factory.CategoriaA, 100, Mes, Mes, null, null));
        Assert.Equal(HttpStatusCode.OK, criada.StatusCode);
        var id = (await criada.Content.ReadFromJsonAsync<RespostaApi<DespesaIdResponse>>())!.Resultado!.Id;
        var url = Base + $"/{id}";
        Assert.Equal(HttpStatusCode.OK, (await _client.PutAsJsonAsync(url, new SalvarDespesaRequest("Editada", _factory.CategoriaA, 110, Mes, Mes, null, null, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync(url + "/pagar", new PagarDespesaRequest(Mes, 100, 1))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync(url + "/pagar", new PagarDespesaRequest(Mes, 105, 2))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync(url + "/cancelar", new VersaoDespesaRequest(3))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync(url + "/estornar", new EstornarDespesaRequest("Correção", 3))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync(url + "/cancelar", new VersaoDespesaRequest(4))).StatusCode);
        var conta = (await _client.GetFromJsonAsync<RespostaApi<DespesaDetalheResponse>>(url))!.Resultado!;
        Assert.Equal(StatusDespesaContrato.Cancelado, conta.Conta.Status); Assert.True(conta.Pagamentos.Single().Estornado);
        var invalida = await _client.PostAsJsonAsync(Base, new SalvarDespesaRequest("", Guid.Empty, -1, Mes.AddDays(2), default, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);
        var erro = (await invalida.Content.ReadFromJsonAsync<RespostaApi<object>>())!.Erro!;
        Assert.Equal("validacao", erro.Codigo); Assert.Contains("Dados.Descricao", erro.Detalhes!.Keys);
    }
    [Theory]
    [InlineData("tamanhoPagina=100")]
    [InlineData("pagina=0")]
    [InlineData("status=99")]
    [InlineData("origem=99")]
    [InlineData("competencia=2026-09-03")]
    public async Task FiltrosInvalidos_Retornam400(string query) => Assert.Equal(HttpStatusCode.BadRequest, (await _client.GetAsync(Base + "?" + query)).StatusCode);
    [Fact]
    public async Task Recorrencia_CriaContaInicial_UmaVez_ECategoriaAtivaProtegida()
    {
        var mes = new DateOnly(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var resposta = await _client.PostAsJsonAsync(Base + "/recorrencias", new SalvarRecorrenciaRequest("Internet", _factory.CategoriaA, 189.90m, 31, mes, null, null, null));
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var id = (await resposta.Content.ReadFromJsonAsync<RespostaApi<DespesaIdResponse>>())!.Resultado!.Id;
        var regra = (await _client.GetFromJsonAsync<RespostaApi<RecorrenciaDespesaResponse>>(Base + $"/recorrencias/{id}"))!.Resultado!;
        Assert.Equal(mes.AddMonths(1), regra.ProximaCompetencia);
        var contas = (await _client.GetFromJsonAsync<RespostaApi<DespesasResponse>>(Base + $"?competencia={mes:yyyy-MM-dd}"))!.Resultado!.Contas.Itens;
        Assert.Single(contas, x => x.RecorrenciaId == id);
        Assert.Equal(HttpStatusCode.Conflict, (await _client.PutAsJsonAsync(Base + $"/categorias/{_factory.CategoriaA}", new CategoriaDespesaRequest("Aluguel", false, 1))).StatusCode);
    }
    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly Dictionary<string, string?> _original = new();
        public Guid A { get; } = Guid.NewGuid(); public Guid B { get; } = Guid.NewGuid();
        public Guid CategoriaA { get; private set; }
        public Guid CategoriaB { get; private set; }
        public Guid ContaB { get; private set; }
        public Guid RegraB { get; private set; }
        public Factory()
        {
            foreach (var (key, value) in new Dictionary<string, string>
            {
                ["Jwt__Emissor"] = "Detara.Tests",
                ["Jwt__Audiencia"] = "Detara.Tests",
                ["Jwt__ChaveAssinatura"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)),
                ["Jwt__ExpiracaoMinutos"] = "60",
                ["ConnectionStrings__DefaultConnection"] = "Data Source=unused"
            })
            { _original[key] = Environment.GetEnvironmentVariable(key); Environment.SetEnvironmentVariable(key, value); }
            _connection.Open();
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing"); builder.ConfigureLogging(x => x.ClearProviders());
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<DbContextOptions<DetaraDbContext>>(); s.RemoveAll<IDbContextOptionsConfiguration<DetaraDbContext>>(); s.RemoveAll<DetaraDbContext>();
                s.RemoveAll<IHostedService>();
                s.AddDbContext<DetaraDbContext>(o => o.UseSqlite(_connection));
                s.AddAuthentication(o => { o.DefaultAuthenticateScheme = "DespesasTest"; o.DefaultChallengeScheme = "DespesasTest"; o.DefaultForbidScheme = "DespesasTest"; })
                    .AddScheme<AuthenticationSchemeOptions, Auth>("DespesasTest", _ => { });
            });
        }
        public async Task PrepararAsync()
        {
            using var scope = Services.CreateScope(); var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
            await using var sistema = new DetaraDbContext(options, new Usuario(Guid.Empty)); await sistema.Database.EnsureCreatedAsync();
            foreach (var id in new[] { A, B })
            {
                var empresa = new Empresa("Teste", "Teste", id == A ? "11111111000111" : "22222222000122", id.ToString("N"));
                typeof(EntidadeBase).GetProperty(nameof(EntidadeBase.Id))!.SetValue(empresa, id); sistema.Add(empresa);
            }
            await sistema.SaveChangesAsync();
            foreach (var id in new[] { A, B })
            {
                await using var db = new DetaraDbContext(options, new Usuario(id));
                var c = new CategoriaDespesa(id, "Aluguel"); var r = new DespesaRecorrente(id, "Aluguel", c, 4500, 10, Mes, null, Mes);
                var conta = new ContaPagar(id, "Conta " + id, c, 100, Mes, Mes);
                db.AddRange(c, r, conta); await db.SaveChangesAsync();
                if (id == A) CategoriaA = c.Id; else { CategoriaB = c.Id; ContaB = conta.Id; RegraB = r.Id; }
            }
        }
        public override async ValueTask DisposeAsync() { await base.DisposeAsync(); await _connection.DisposeAsync(); foreach (var (k, v) in _original) Environment.SetEnvironmentVariable(k, v); }
    }
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Tenant"], out var tenant)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new("sub", tenant.ToString()), new("empresa_id", tenant.ToString()) };
            claims.AddRange(Request.Headers["X-Permissions"].ToString().Split(',').Select(p => new Claim("permissao", p)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
    private sealed class Usuario(Guid empresa) : IUsuarioContexto
    { public Guid EmpresaId => empresa; public Guid UsuarioId => empresa; public bool EstaAutenticado => empresa != Guid.Empty; }
}
