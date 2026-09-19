using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Capacidades;
using Detara.Contracts.Comum;
using Detara.Web.Servicos;

namespace Detara.UnitTests;

public sealed class EmpresaCapacidadesStateTests
{
    [Fact]
    public async Task SnapshotCarregado_ExpoeCapacidadeESoConsultaUmaVez()
    {
        var handler = new RespostaHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(RespostaApi<SnapshotCapacidadesEmpresaResponse>.Ok(new(
                "estetica-automotiva",
                [new(CodigosCapacidadeContrato.Veiculos, "Veículos", "Automotivo", true, false, 90)])))
        });
        var state = new EmpresaCapacidadesState(new HttpClient(handler) { BaseAddress = new Uri("https://detara.test/") });

        await Task.WhenAll(state.CarregarAsync(), state.CarregarAsync());

        Assert.Equal(EstadoCarregamentoCapacidades.Carregado, state.Estado);
        Assert.True(state.Possui(CodigosCapacidadeContrato.Veiculos));
        Assert.False(state.Possui("desconhecida"));
        Assert.Equal(1, handler.Chamadas);
    }

    [Fact]
    public async Task FalhaDeRede_FalhaFechadaESemLiberarModulo()
    {
        var state = new EmpresaCapacidadesState(new HttpClient(new RespostaHandler(_ =>
            throw new HttpRequestException("indisponível")))
        { BaseAddress = new Uri("https://detara.test/") });

        await state.CarregarAsync();

        Assert.Equal(EstadoCarregamentoCapacidades.FalhaRede, state.Estado);
        Assert.False(state.Possui(CodigosCapacidadeContrato.Veiculos));
    }

    [Fact]
    public async Task Unauthorized_EhDistintoDeFalhaDeRede()
    {
        var state = new EmpresaCapacidadesState(new HttpClient(new RespostaHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Unauthorized)))
        { BaseAddress = new Uri("https://detara.test/") });

        await state.CarregarAsync();

        Assert.Equal(EstadoCarregamentoCapacidades.SessaoInvalida, state.Estado);
    }

    private sealed class RespostaHandler(Func<HttpRequestMessage, HttpResponseMessage> resposta) : HttpMessageHandler
    {
        public int Chamadas { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Chamadas++;
            return Task.FromResult(resposta(request));
        }
    }
}
