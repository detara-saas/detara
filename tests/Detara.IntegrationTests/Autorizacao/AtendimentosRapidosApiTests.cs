using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Contracts.Financeiro;
using Detara.Domain.Atendimento;
using Detara.Domain.Capacidades;
using Detara.Domain.Entidades;
using Detara.Domain.Financeiro;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.IntegrationTests.Autorizacao;

public sealed partial class ClientesVeiculosAutorizacaoTests
{
    [Fact]
    public async Task AtendimentoRapido_RegistraHistoricoContaPagaEPreservacaoDoPrecoNegociado()
    {
        PermitirAtendimentoRapido();
        var request = LancamentoRapido();
        var response = await _client.PostAsJsonAsync("/api/atendimentos-rapidos", request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var resultado = (await response.Content.ReadFromJsonAsync<RespostaApi<AtendimentoRapidoResponse>>())!.Resultado!;

        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
        await using var db = new DetaraDbContext(options, new TestUserContext(_factory.EmpresaId));
        var ordem = await db.OrdensServico.Include(x => x.Historico).Include(x => x.Itens)
            .SingleAsync(x => x.Id == resultado.OrdemServicoId);
        var conta = await db.ContasReceber.Include(x => x.Pagamentos).SingleAsync(x => x.Id == resultado.ContaReceberId);
        Assert.Equal(StatusOrdemServico.Concluida, ordem.Status);
        Assert.Equal(OrigemOrdemServico.AtendimentoRapido, ordem.Origem);
        Assert.Equal(request.ValorPago, ordem.TotalAutorizado);
        Assert.Equal(_factory.VeiculoId, ordem.VeiculoId);
        Assert.Null(ordem.CheckInEmUtc);
        Assert.Null(ordem.IniciadaEmUtc);
        Assert.Null(ordem.AgendamentoOrigemId);
        Assert.Single(ordem.Historico);
        Assert.Equal(1, Assert.Single(ordem.Itens).Quantidade);
        Assert.Equal(StatusContaReceber.Pago, conta.Status);
        Assert.Equal(request.ValorPago, conta.ValorOriginal);
        Assert.Equal(request.ValorPago, conta.ValorRecebido);
        Assert.Equal(0, conta.ValorEmAberto);
        Assert.Equal(DateOnly.FromDateTime(request.RealizadoEmLocal), conta.DataCompetencia);
        Assert.Equal(Detara.Domain.Financeiro.FormaPagamento.Pix, Assert.Single(conta.Pagamentos).FormaPagamento);
        Assert.Equal(0, await db.Agendamentos.CountAsync());
        Assert.Equal(0, await db.NotificacoesEmail.CountAsync());
    }

    [Fact]
    public async Task AtendimentoRapido_RepeticaoRetornaMesmoRegistroERejeitaMudancaDePayload()
    {
        PermitirAtendimentoRapido();
        var request = LancamentoRapido();
        var primeiro = await _client.PostAsJsonAsync("/api/atendimentos-rapidos", request);
        var repetido = await _client.PostAsJsonAsync("/api/atendimentos-rapidos", request);
        Assert.Equal(HttpStatusCode.OK, repetido.StatusCode);
        Assert.Equal((await primeiro.Content.ReadFromJsonAsync<RespostaApi<AtendimentoRapidoResponse>>())!.Resultado,
            (await repetido.Content.ReadFromJsonAsync<RespostaApi<AtendimentoRapidoResponse>>())!.Resultado);
        var alterado = await _client.PostAsJsonAsync("/api/atendimentos-rapidos", request with { ValorPago = 1m });
        Assert.Equal(HttpStatusCode.Conflict, alterado.StatusCode);
        await AssertQuantidadeRapidaAsync(1);
    }

    [Theory]
    [InlineData(Permissoes.OrdemServicoCriar)]
    [InlineData(Permissoes.OrdemServicoFinalizar)]
    [InlineData(Permissoes.FinanceiroRegistrarPagamento)]
    public async Task AtendimentoRapido_ExigeTodasAsPermissoes(string ausente)
    {
        UsarPermissoes(new[] { Permissoes.OrdemServicoCriar, Permissoes.OrdemServicoFinalizar,
            Permissoes.FinanceiroRegistrarPagamento }.Where(x => x != ausente).ToArray());
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.PostAsJsonAsync("/api/atendimentos-rapidos", LancamentoRapido())).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.GetAsync("/api/atendimentos-rapidos/clientes?pesquisa=Cliente")).StatusCode);
        await AssertQuantidadeRapidaAsync(0);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1.001)]
    [InlineData(1000000000)]
    public async Task AtendimentoRapido_ValorInvalidoRetornaValidacaoSemEscrita(decimal valor)
    {
        PermitirAtendimentoRapido();
        var response = await _client.PostAsJsonAsync("/api/atendimentos-rapidos", LancamentoRapido() with { ValorPago = valor });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var envelope = await response.Content.ReadFromJsonAsync<RespostaApi<object>>();
        Assert.Equal("validacao", envelope!.Erro!.Codigo);
        Assert.Contains("ValorPago", envelope.Erro.Detalhes!.Keys);
        await AssertQuantidadeRapidaAsync(0);
    }

    [Theory]
    [InlineData("cliente")]
    [InlineData("veiculo")]
    [InlineData("servico")]
    public async Task AtendimentoRapido_RejeitaIdentificadoresCrossTenant(string campo)
    {
        PermitirAtendimentoRapido();
        var request = LancamentoRapido();
        if (campo == "cliente") request = request with { ClienteId = _factory.ClienteOutroTenantId };
        if (campo == "servico") request = request with { ItemCatalogoId = _factory.ServicoOutroTenantId };
        if (campo == "veiculo")
        {
            using var scope = _factory.Services.CreateScope();
            var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
            await using var db = new DetaraDbContext(options, new TestUserContext(_factory.EmpresaOutroTenantId));
            var veiculo = new Veiculo(_factory.EmpresaOutroTenantId, _factory.ClienteOutroTenantId,
                "DEF2G34", "Fiat", "Uno", null, null, null, null, null, null);
            db.Veiculos.Add(veiculo);
            await db.SaveChangesAsync();
            request = request with { VeiculoId = veiculo.Id };
        }
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.PostAsJsonAsync("/api/atendimentos-rapidos", request)).StatusCode);
        await AssertQuantidadeRapidaAsync(0);
    }

    [Fact]
    public async Task AtendimentoRapido_ConsultasNaoExpoemOutroTenant()
    {
        PermitirAtendimentoRapido();
        var clientes = await _client.GetFromJsonAsync<RespostaApi<ClienteOrcamentoResponse[]>>(
            "/api/atendimentos-rapidos/clientes?pesquisa=Cliente");
        Assert.DoesNotContain(clientes!.Resultado!, x => x.Id == _factory.ClienteOutroTenantId);
        var catalogo = await _client.GetFromJsonAsync<RespostaApi<ItemCatalogoOrcamentoResponse[]>>(
            "/api/atendimentos-rapidos/catalogo");
        Assert.DoesNotContain(catalogo!.Resultado!, x => x.Id == _factory.ServicoOutroTenantId);
        var veiculos = await _client.GetFromJsonAsync<RespostaApi<VeiculoOrcamentoResponse[]>>(
            $"/api/atendimentos-rapidos/clientes/{_factory.ClienteOutroTenantId}/veiculos");
        Assert.Empty(veiculos!.Resultado!);
    }

    [Fact]
    public async Task AtendimentoRapido_CapabilityCondicionaVeiculo()
    {
        PermitirAtendimentoRapido();
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/atendimentos-rapidos",
            LancamentoRapido() with { VeiculoId = null })).StatusCode);
        await _factory.DefinirCapacidadeAsync(CodigosCapacidadeEmpresa.CheckIn, false);
        await _factory.DefinirCapacidadeAsync(CodigosCapacidadeEmpresa.Veiculos, false);
        Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/atendimentos-rapidos", LancamentoRapido())).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.PostAsJsonAsync("/api/atendimentos-rapidos",
            LancamentoRapido() with { VeiculoId = null })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _client.GetAsync(
            $"/api/atendimentos-rapidos/clientes/{_factory.ClienteId}/veiculos")).StatusCode);
        await AssertQuantidadeRapidaAsync(1);
    }

    [Fact]
    public async Task AtendimentoRapido_RejeitaPagamentoInvalidoEDataFuturaSemGerarOS()
    {
        PermitirAtendimentoRapido();
        foreach (var request in new[]
        {
            LancamentoRapido() with { RealizadoEmLocal = DateTime.SpecifyKind(DateTime.UtcNow.AddDays(2), DateTimeKind.Unspecified) },
            LancamentoRapido() with { FormaPagamento = FormaPagamentoContrato.Outro },
            LancamentoRapido() with { FormaPagamento = FormaPagamentoContrato.CartaoCredito },
            LancamentoRapido() with { Taxa = 200 },
            LancamentoRapido() with { NumeroParcelas = 2 },
            LancamentoRapido() with { FormaPagamento = (FormaPagamentoContrato)999 }
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await _client.PostAsJsonAsync("/api/atendimentos-rapidos", request)).StatusCode);
        await AssertQuantidadeRapidaAsync(0);
    }

    private void PermitirAtendimentoRapido() => UsarPermissoes(Permissoes.OrdemServicoCriar,
        Permissoes.OrdemServicoFinalizar, Permissoes.FinanceiroRegistrarPagamento);

    private RegistrarAtendimentoRapidoRequest LancamentoRapido() => new(Guid.NewGuid(), _factory.ClienteId,
        _factory.VeiculoId, TipoItemOrcamentoContrato.Servico, _factory.ServicoId, 137.50m,
        FormaPagamentoContrato.Pix, DateTime.SpecifyKind(DateTime.UtcNow.AddDays(-1), DateTimeKind.Unspecified));

    private async Task AssertQuantidadeRapidaAsync(int quantidade)
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
        await using var db = new DetaraDbContext(options, new TestUserContext(_factory.EmpresaId));
        Assert.Equal(quantidade, await db.OrdensServico.CountAsync());
        Assert.Equal(quantidade, await db.ContasReceber.CountAsync());
        Assert.Equal(quantidade, await db.Pagamentos.CountAsync());
    }
}
