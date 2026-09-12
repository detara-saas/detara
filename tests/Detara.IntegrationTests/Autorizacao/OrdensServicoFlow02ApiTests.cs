using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Autorizacao;
using Detara.Contracts.Comum;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.IntegrationTests.Autorizacao;

public sealed partial class ClientesVeiculosAutorizacaoTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task Flow02_Api_ResolveOrigemECriacaoComErrosPadrao(int aprovados)
    {
        var agenda = await PrepararAgendaFlow02Async(_factory.EmpresaId, aprovados);
        UsarPermissoes(Permissoes.OrdemServicoCriar);
        var preview = await _client.GetAsync($"/api/ordens-servico/agendamentos/{agenda}/origem-comercial");
        var request = new CriarOrdemServicoRequest(null, agenda, null, null, null, 0, 0, null, []);
        var response = await _client.PostAsJsonAsync("/api/ordens-servico", request);
        if (aprovados == 2)
        {
            Assert.Equal(HttpStatusCode.Conflict, preview.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            var erro = await response.Content.ReadFromJsonAsync<RespostaApi<OrdemServicoDetalheResponse>>();
            Assert.False(erro!.Sucesso);
            Assert.Contains("mais de um orçamento aprovado", erro.Info);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            var origem = (await preview.Content.ReadFromJsonAsync<RespostaApi<OrigemComercialOrdemServicoResponse>>())!.Resultado!;
            if (aprovados == 0)
            {
                Assert.Null(origem.OrcamentoId);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Contains("Itens", await response.Content.ReadAsStringAsync());
            }
            else
            {
                Assert.Equal(HttpStatusCode.Created, response.StatusCode);
                var ordem = (await response.Content.ReadFromJsonAsync<RespostaApi<OrdemServicoDetalheResponse>>())!.Resultado!;
                Assert.Equal(120m, ordem.TotalAutorizado);
                Assert.Equal(origem.OrcamentoId, ordem.OrcamentoOrigemId);
                Assert.Equal(agenda, ordem.AgendamentoOrigemId);
                Assert.Equal(HttpStatusCode.Conflict, (await _client.PostAsJsonAsync("/api/ordens-servico", request)).StatusCode);
            }
        }
    }

    [Fact]
    public async Task Flow02_Api_PreviewExigeCriarENaoExpoeOutroTenant()
    {
        var agendaB = await PrepararAgendaFlow02Async(_factory.EmpresaOutroTenantId, 0);
        Assert.Equal(HttpStatusCode.Forbidden,
            (await _client.GetAsync($"/api/ordens-servico/agendamentos/{agendaB}/origem-comercial")).StatusCode);
        UsarPermissoes(Permissoes.OrdemServicoCriar);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _client.GetAsync($"/api/ordens-servico/agendamentos/{agendaB}/origem-comercial")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.PostAsJsonAsync("/api/ordens-servico",
            new CriarOrdemServicoRequest(null, agendaB, null, null, null, 0, 0, null, []))).StatusCode);
    }

    private async Task<Guid> PrepararAgendaFlow02Async(Guid empresaId, int aprovados)
    {
        using var scope = _factory.Services.CreateScope();
        var options = scope.ServiceProvider.GetRequiredService<DbContextOptions<DetaraDbContext>>();
        await using var db = new DetaraDbContext(options, new TestUserContext(empresaId));
        var agenda = new Agendamento(empresaId, _factory.ClienteId, "QA FLOW-02", _factory.VeiculoId,
            "Honda Civic", "ABC1D23", DateTime.UtcNow.AddDays(1), 90, null, null,
            [new(TipoItemAgendamento.Servico, _factory.ServicoId, "Lavagem", null, TipoPrecificacao.Fixo, 150m, 90)]);
        db.Agendamentos.Add(agenda);
        for (var i = 0; i < aprovados; i++)
        {
            var usuario = Guid.NewGuid();
            var orcamento = new Orcamento(empresaId, new(_factory.ClienteId, "QA FLOW-02", null, null,
                _factory.VeiculoId, "Honda Civic", "ABC1D23"), agenda.Id, null,
                DateOnly.FromDateTime(DateTime.UtcNow).AddDays(7), null, null, null, 0, 0,
                [new(TipoItemOrcamento.Servico, _factory.ServicoId, "Lavagem", null, TipoPrecificacao.Fixo, 150m, 120m, 1, 1, null)], usuario);
            orcamento.Emitir(2026, usuario);
            orcamento.Aprovar(DateOnly.FromDateTime(DateTime.UtcNow), usuario, null);
            db.Orcamentos.Add(orcamento);
        }
        await db.SaveChangesAsync();
        return agenda.Id;
    }
}
