using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using Detara.Application.Abstracoes;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Relatorios;
using Detara.Domain.Entidades;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Domain.Agenda;
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

namespace Detara.IntegrationTests.Relatorios;

[Collection("api-security")]
public sealed class RelatoriosApiTests : IAsyncLifetime
{
    private readonly Factory _factory = new();
    private HttpClient _client = null!;
    private const string Periodo = "?periodo=7&inicio=2026-09-01&fim=2026-09-30";
    private static readonly DateTime Data = new(2026, 9, 7, 12, 0, 0, DateTimeKind.Utc);
    public async Task InitializeAsync() { _client = _factory.CreateClient(); await _factory.PrepararAsync(); Autorizar(); }
    public async Task DisposeAsync() { _client.Dispose(); await _factory.DisposeAsync(); }
    private void Autorizar(params string[] permissoes)
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant"); _client.DefaultRequestHeaders.Add("X-Tenant", _factory.A.ToString());
        _client.DefaultRequestHeaders.Remove("X-Permissions");
        _client.DefaultRequestHeaders.Add("X-Permissions", string.Join(',', permissoes.Length == 0 ?
            [Permissoes.FinanceiroVisualizar, Permissoes.OrdemServicoVisualizar, Permissoes.OrcamentosVisualizar, Permissoes.ClientesVisualizar, Permissoes.AgendaVisualizar] : permissoes));
    }
    private async Task<RelatorioResponse> Obter(int perspectiva, string? periodo = null)
    {
        var response = await _client.GetAsync($"/api/relatorios/{perspectiva}{periodo ?? Periodo}");
        var texto = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, texto);
        return (await response.Content.ReadFromJsonAsync<RespostaApi<RelatorioResponse>>())!.Resultado!;
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task TodasPerspectivas_AnonimoESemPermissao(int perspectiva)
    {
        _client.DefaultRequestHeaders.Remove("X-Tenant");
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.GetAsync($"/api/relatorios/{perspectiva}{Periodo}")).StatusCode);
        Autorizar("sem-permissao");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync($"/api/relatorios/{perspectiva}{Periodo}")).StatusCode);
    }
    [Fact]
    public async Task Caixa_DatasReais_ValorPagoEstornosESaldos()
    {
        var r = await Obter(4); var f = r.Financeiro!;
        Assert.Equal(500, f.Receita); Assert.Equal(400, f.Despesas); Assert.Equal(100, f.Resultado);
        Assert.Equal(2, f.Recebimentos); Assert.Equal(1, f.Pagamentos);
        Assert.Equal(700, f.APagar); Assert.Equal(700, f.Vencido); Assert.Equal(150, f.AReceber);
        Assert.Equal(500, f.Serie.Sum(x => x.Receita)); Assert.Equal(400, f.Serie.Sum(x => x.Despesa));
        Assert.Equal(400, Assert.Single(f.Categorias).Valor);
        Assert.Null(r.Atendimento); Assert.Empty(r.Insights);
        var agosto = await Obter(4, "?periodo=7&inicio=2026-08-01&fim=2026-08-31");
        Assert.Equal(1000, agosto.Financeiro!.Receita); Assert.Equal(0, agosto.Financeiro.Despesas);
    }
    [Fact]
    public async Task Concluidas_TicketDistinct_CortesiaSnapshots_EJoinNaoMultiplica()
    {
        var r = await Obter(2); var a = r.Atendimento!;
        Assert.Equal(2, a.Concluidos); Assert.Equal(1500, a.Valor); Assert.Equal(750, a.Ticket);
        Assert.Equal(2, a.Clientes); Assert.Equal(1, a.Novos); Assert.Equal(1, a.Recorrentes);
        Assert.Equal(6, a.QuantidadeServicos);
        var principal = a.ServicosQuantidade[0]; Assert.Equal(4, principal.Quantidade); Assert.Equal(1400, principal.Valor);
        Assert.Equal(3, a.ServicosQuantidade.Count); Assert.Contains(a.ServicosQuantidade, x => x.Valor == 0 && x.Quantidade == 1);
        Assert.Equal(1500, a.ServicosValor.Sum(x => x.Valor)); Assert.Null(r.Financeiro);
    }
    [Fact]
    public async Task Clientes_ConsumoFrequenciaSemPII()
    {
        var r = await Obter(3);
        Assert.Equal("Cliente novo A", r.Atendimento!.ClientesConsumo[0].Nome);
        Assert.Equal(1000, r.Atendimento.ClientesConsumo[0].Valor);
        Assert.All(r.Atendimento.ClientesFrequencia, x => Assert.Equal(1, x.Quantidade));
        Assert.Empty(r.Atendimento.ServicosValor); Assert.Null(r.Financeiro);
    }
    [Fact]
    public async Task AgendaEDiaLocal_SomenteStatusReais()
    {
        var r = await Obter(5);
        Assert.Equal(3, r.Agenda!.Agendamentos); Assert.Equal(1, r.Agenda.Cancelados); Assert.Equal(1, r.Agenda.NaoCompareceu);
        Assert.Equal(2, r.Atendimento!.Dias.Sum(x => x.Atendimentos));
        Assert.Contains(r.Atendimento.Dias, x => x.Data == new DateOnly(2026, 9, 30));
    }
    [Fact]
    public async Task Orcamentos_DecisaoExcluiPendentesESubstituidos()
    {
        var o = (await Obter(2)).Orcamentos!;
        Assert.Equal(8, o.Aprovados); Assert.Equal(2, o.Recusados); Assert.Equal(80, o.Conversao);
        Assert.Equal(18, o.Criados);
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public async Task TenantB_NuncaContaminaCardsSeriesRankingsInsightsOuComparacoes(int perspectiva)
    {
        var a = await Obter(perspectiva, Periodo + $"&empresaId={_factory.B}");
        var texto = System.Text.Json.JsonSerializer.Serialize(a);
        Assert.DoesNotContain("Tenant secreto B", texto);
        if (a.Financeiro is { } f) { Assert.Equal(500, f.Receita); Assert.Equal(-50, a.VariacaoReceita); }
        if (a.Atendimento is { } at) { Assert.Equal(2, at.Concluidos); Assert.Equal(1500, at.Valor); Assert.Equal(50, a.VariacaoTicket); }
        Assert.DoesNotContain(a.Insights, x => x.Nome.Contains("secreto"));
    }
    [Fact]
    public async Task SemFinanceiro_NaoConsultaNemRetornaCaixa()
    {
        Autorizar(Permissoes.OrdemServicoVisualizar);
        var geral = await Obter(1); Assert.Null(geral.Financeiro); Assert.Null(geral.VariacaoReceita);
        Assert.DoesNotContain(geral.Insights, x => x.Tipo is "receita" or "categoria" or "resultado-negativo");
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/relatorios/4" + Periodo)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/relatorios/3" + Periodo)).StatusCode);
        Assert.Empty(geral.Atendimento!.ClientesConsumo);
    }
    [Fact]
    public async Task SomenteFinanceiro_NaoExibeComercialOuClientes()
    {
        Autorizar(Permissoes.FinanceiroVisualizar);
        var geral = await Obter(1); Assert.Null(geral.Atendimento); Assert.Null(geral.Orcamentos); Assert.Null(geral.VariacaoTicket);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync("/api/relatorios/3" + Periodo)).StatusCode);
    }
    [Theory]
    [InlineData("?periodo=7&inicio=2026-10-01&fim=2026-09-01")]
    [InlineData("?periodo=7&inicio=2026-09-01")]
    [InlineData("?periodo=7&inicio=2000-01-01&fim=2026-09-01")]
    [InlineData("?periodo=999")]
    public async Task DatasManipuladas_RetornamErroPadrao(string filtro)
    {
        var r = await _client.GetAsync("/api/relatorios/4" + filtro);
        Assert.False(r.IsSuccessStatusCode); Assert.NotEqual(HttpStatusCode.InternalServerError, r.StatusCode);
        Assert.False((await r.Content.ReadFromJsonAsync<RespostaApi<RelatorioResponse>>())!.Sucesso);
    }
    [Fact]
    public async Task VazioNaoInventaMovimentos()
    {
        var r = await Obter(1, "?periodo=7&inicio=2025-01-01&fim=2025-01-02");
        Assert.Equal(0, r.Financeiro!.Receita); Assert.Equal(0, r.Financeiro.Recebimentos); Assert.Empty(r.Financeiro.Serie);
        Assert.Empty(r.Insights); Assert.Equal(0, r.Atendimento!.Concluidos);
    }

    [Fact]
    public async Task Rankings_Top10DesempateEstavel_ECategoriasComOutras()
    {
        await _factory.ComplementarRankingsAsync();
        var servicos = (await Obter(2)).Atendimento!;
        Assert.Equal(10, servicos.ServicosQuantidade.Count); Assert.Equal(10, servicos.ServicosValor.Count);
        Assert.Equal(servicos.ServicosValor, (await Obter(2)).Atendimento!.ServicosValor);
        var clientes = (await Obter(3)).Atendimento!;
        Assert.Equal(10, clientes.ClientesConsumo.Count); Assert.Equal(10, clientes.ClientesFrequencia.Count);
        var f = (await Obter(4)).Financeiro!;
        Assert.Equal(11, f.Categorias.Count); Assert.Equal("Outras categorias", f.Categorias[^1].Nome);
        Assert.Equal(f.Despesas, f.Categorias.Sum(x => x.Valor));
    }

    private sealed class Factory : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        private readonly Dictionary<string, string?> _original = new();
        public Guid A { get; } = Guid.NewGuid(); public Guid B { get; } = Guid.NewGuid();
        public Factory()
        {
            foreach (var (k, v) in new Dictionary<string, string>
            { ["Jwt__Emissor"] = "Detara.Tests", ["Jwt__Audiencia"] = "Detara.Tests", ["Jwt__ChaveAssinatura"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48)), ["Jwt__ExpiracaoMinutos"] = "60", ["ConnectionStrings__DefaultConnection"] = "Data Source=unused" })
            { _original[k] = Environment.GetEnvironmentVariable(k); Environment.SetEnvironmentVariable(k, v); }
            _connection.Open();
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing"); builder.ConfigureLogging(x => x.ClearProviders());
            builder.ConfigureTestServices(s =>
            {
                s.RemoveAll<DbContextOptions<DetaraDbContext>>(); s.RemoveAll<IDbContextOptionsConfiguration<DetaraDbContext>>(); s.RemoveAll<DetaraDbContext>(); s.RemoveAll<IHostedService>();
                s.AddDbContext<DetaraDbContext>(o => o.UseSqlite(_connection));
                s.RemoveAll<TimeProvider>(); s.AddSingleton<TimeProvider>(new Relogio());
                s.AddAuthentication(o => { o.DefaultAuthenticateScheme = "RelatoriosTest"; o.DefaultChallengeScheme = "RelatoriosTest"; o.DefaultForbidScheme = "RelatoriosTest"; })
                    .AddScheme<AuthenticationSchemeOptions, Auth>("RelatoriosTest", _ => { });
            });
        }
        public async Task PrepararAsync()
        {
            using var scope = Services.CreateScope(); var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
            await using var sistema = new DetaraDbContext(options, new Usuario(Guid.Empty)); await sistema.Database.EnsureCreatedAsync();
            foreach (var id in new[] { A, B })
            {
                var empresa = new Empresa("Teste", "Teste", id == A ? "11111111000111" : "22222222000122", id.ToString("N"));
                Definir(empresa, nameof(EntidadeBase.Id), id); sistema.Add(empresa);
            }
            await sistema.SaveChangesAsync();
            foreach (var id in new[] { A, B })
            {
                await using var db = new DetaraDbContext(options, new Usuario(id));
                await Semear(db, id, id == A ? 1 : 100);
            }
        }
        public async Task ComplementarRankingsAsync()
        {
            using var scope = Services.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
            await using var db = new DetaraDbContext(options, new Usuario(A));
            for (var i = 0; i < 12; i++)
            {
                var os = new OrdemServico(A, 2026, new(Guid.NewGuid(), $"Cliente extenso para ranking {i}", null, null, Guid.NewGuid(), "Veículo", null),
                    OrigemOrdemServico.AtendimentoDireto, null, null, 60, 0, 0,
                    [new(TipoItemOrcamento.Servico, Guid.NewGuid(), null, null, $"Serviço extenso {i}", null, 100, 2, 1, OrigemComercialOrdemServico.AcordoDireto, Data, A, null)], A, Data);
                Definir(os, nameof(os.Status), StatusOrdemServico.Concluida); Definir(os, nameof(os.ConcluidaEmUtc), Data); db.Add(os);
                var c = new CategoriaDespesa(A, $"Categoria {i}"); var despesa = new ContaPagar(A, "Despesa", c, 100, new(2026, 9, 1), new(2026, 9, 7));
                despesa.RegistrarPagamento(new(2026, 9, 7), 100, A, Data); db.AddRange(c, despesa);
            }
            await db.SaveChangesAsync();
        }
        public override async ValueTask DisposeAsync() { await base.DisposeAsync(); await _connection.DisposeAsync(); foreach (var (k, v) in _original) Environment.SetEnvironmentVariable(k, v); }
    }
    private static async Task Semear(DetaraDbContext db, Guid tenant, int fator)
    {
        var cliente = Guid.NewGuid(); var servico = Guid.NewGuid(); var usuario = tenant;
        var nome = fator == 1 ? "Cliente recorrente A" : "Tenant secreto B";
        OrdemServico Ordem(Guid clienteId, string clienteNome, DateTime data, decimal preco, int quantidade, bool anterior = false)
        {
            var itens = new List<ItemOrdemServicoSnapshot> { new(TipoItemOrcamento.Servico, servico, null, null, anterior ? "Lavagem antiga" : "Lavagem histórica", null, preco * fator, quantidade, 1, OrigemComercialOrdemServico.AcordoDireto, data.AddHours(-1), usuario, null) };
            if (preco == 200) { itens.Add(new(TipoItemOrcamento.Personalizado, null, null, null, "Adicional", null, 100 * fator, 1, 2, OrigemComercialOrdemServico.AcordoDireto, data, usuario, null)); itens.Add(new(TipoItemOrcamento.Personalizado, null, null, null, "Cortesia", null, 0, 1, 3, OrigemComercialOrdemServico.Cortesia, data, usuario, null)); }
            var os = new OrdemServico(tenant, 2026, new(clienteId, clienteNome, null, null, Guid.NewGuid(), "Veículo", null), OrigemOrdemServico.AtendimentoDireto, null, null, 60, 0, 0, itens, usuario, data.AddHours(-1));
            os.RealizarCheckIn(new(NivelExigenciaOperacional.Desabilitado, NivelExigenciaOperacional.Desabilitado, NivelExigenciaOperacional.Desabilitado, null, []), null, null, usuario);
            os.IniciarExecucao(usuario, null); os.FinalizarExecucao(usuario, null); os.Concluir(usuario, null);
            Definir(os, nameof(os.ConcluidaEmUtc), data); db.Add(os); return os;
        }
        var primeira = Ordem(cliente, nome, Data, 200, 2);
        Ordem(cliente, nome, Data.AddMonths(-1), 500, 1, true);
        Ordem(Guid.NewGuid(), fator == 1 ? "Cliente novo A" : nome, new(2026, 10, 1, 2, 59, 59, DateTimeKind.Utc), 500, 2);
        var fora = Ordem(Guid.NewGuid(), "Fora do período", new(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc), 10000, 1);
        var aberta = Ordem(Guid.NewGuid(), "Aberta", Data, 10000, 1); Definir(aberta, nameof(aberta.Status), StatusOrdemServico.Aberta);
        var cancelada = Ordem(Guid.NewGuid(), "Cancelada", Data, 10000, 1); Definir(cancelada, nameof(cancelada.Status), StatusOrdemServico.Cancelada);
        ContaReceber Conta(decimal valor) { var c = new ContaReceber(tenant, Guid.NewGuid(), "OS-TESTE", cliente, nome, Guid.NewGuid(), "Veículo", null, valor * fator, 0, 0, valor * fator, new(2026, 8, 1)); db.Add(c); return c; }
        var conta = new ContaReceber(tenant, primeira.Id, primeira.Codigo, cliente, nome, primeira.VeiculoId, "Veículo", null, 500 * fator, 0, 0, 500 * fator, new(2026, 8, 1));
        conta.RegistrarPagamento(FormaPagamento.Pix, 200 * fator, 10, null, null, Data, usuario);
        conta.RegistrarPagamento(FormaPagamento.Pix, 300 * fator, 0, null, null, Data, usuario); db.Add(conta);
        Conta(1000).RegistrarPagamento(FormaPagamento.Pix, 1000 * fator, 0, null, null, Data.AddMonths(-1), usuario);
        var estornada = Conta(900); var pg = estornada.RegistrarPagamento(FormaPagamento.Pix, 900 * fator, 0, null, null, Data, usuario); estornada.EstornarPagamento(pg.Id, usuario, "Correção", Data.AddHours(1));
        var parcial = Conta(200); parcial.AlterarVencimento(new(2026, 9, 20)); parcial.RegistrarPagamento(FormaPagamento.Pix, 50 * fator, 0, null, null, Data.AddMonths(-2), usuario);
        var cat = new CategoriaDespesa(tenant, fator == 1 ? "Produtos" : nome); db.Add(cat);
        var paga = new ContaPagar(tenant, "Pago real diferente", cat, 500, new(2026, 8, 1), new(2026, 8, 10)); paga.RegistrarPagamento(new(2026, 9, 7), 400 * fator, usuario, Data); db.Add(paga);
        var pendente = new ContaPagar(tenant, "Pendente", cat, 700 * fator, new(2026, 9, 1), new(2026, 9, 1)); db.Add(pendente);
        var revertida = new ContaPagar(tenant, "Estornada", cat, 200, new(2026, 8, 1), new(2026, 8, 1)); revertida.RegistrarPagamento(new(2026, 9, 7), 200, usuario, Data); revertida.EstornarPagamento(usuario, "Correção", Data); db.Add(revertida);
        cat.DefinirAtividade(false);
        foreach (var status in new[] { StatusAgendamento.Agendado, StatusAgendamento.Cancelado, StatusAgendamento.NaoCompareceu })
        {
            var agenda = new Agendamento(tenant, cliente, nome, Guid.NewGuid(), "Veículo", null, Data, 60, null, null, [new(TipoItemAgendamento.Servico, servico, "Lavagem", null, TipoPrecificacao.Fixo, 100, 60)]);
            if (status != StatusAgendamento.Agendado) agenda.AlterarStatus(status, status == StatusAgendamento.Cancelado ? "Cancelado" : null); db.Add(agenda);
        }
        for (var i = 0; i < 18; i++)
        {
            var orc = new Orcamento(tenant, new(cliente, nome, null, null, Guid.NewGuid(), "Veículo", null), null, null, new(2026, 12, 31), null, null, null, 0, 0,
                [new(TipoItemOrcamento.Personalizado, null, "Lavagem", null, null, null, 100, 1, 1, null)], usuario);
            Definir(orc, nameof(orc.CriadoEmUtc), Data);
            Definir(orc, nameof(orc.Status), i < 8 ? StatusOrcamento.Aprovado : i < 10 ? StatusOrcamento.Recusado : i < 15 ? StatusOrcamento.Emitido : StatusOrcamento.Substituido);
            if (i < 8 || i >= 15) Definir(orc, nameof(orc.AprovadoEmUtc), Data);
            if (i is >= 8 and < 10) Definir(orc, nameof(orc.RecusadoEmUtc), Data);
            db.Add(orc);
        }
        await db.SaveChangesAsync();
        await db.Orcamentos.Where(x => x.EmpresaId == tenant).ExecuteUpdateAsync(s => s.SetProperty(x => x.CriadoEmUtc, Data));
    }
    private static void Definir(object entidade, string nome, object valor) => entidade.GetType().GetProperty(nome)!.SetValue(entidade, valor);
    private sealed class Relogio : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Data); }
    private sealed class Auth(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Guid.TryParse(Request.Headers["X-Tenant"], out var tenant)) return Task.FromResult(AuthenticateResult.NoResult());
            var claims = new List<Claim> { new("sub", tenant.ToString()), new("empresa_id", tenant.ToString()) };
            claims.AddRange(Request.Headers["X-Permissions"].ToString().Split(',').Select(p => new Claim("permissao", p)));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name)), Scheme.Name)));
        }
    }
    private sealed class Usuario(Guid empresa) : IUsuarioContexto { public Guid EmpresaId => empresa; public Guid UsuarioId => empresa; public bool EstaAutenticado => empresa != Guid.Empty; }
}
