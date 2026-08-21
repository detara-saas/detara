using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Plataforma;
using Detara.Web.Seguranca;
using Detara.Web.Servicos;
using Microsoft.JSInterop;

namespace Detara.UnitTests.Seguranca;

public sealed class PlataformaServicoPaginacaoTests
{
    [Fact]
    public async Task ListagemDeEmpresas_UsaTamanhoPaginaPermitidoPorPadrao()
    {
        using var contexto = CriarContexto();

        var resultado = await contexto.Servico.ListarEmpresasAsync(1, null, null);

        Assert.True(resultado.Sucesso);
        Assert.Equal("?pagina=1&tamanhoPagina=25", contexto.Handler.RequestUri?.Query);
    }

    [Fact]
    public async Task ListagemDeAuditoria_UsaTamanhoPaginaPermitidoPorPadrao()
    {
        using var contexto = CriarContexto();

        var resultado = await contexto.Servico.ListarAuditoriaAsync(1, null);

        Assert.True(resultado.Sucesso);
        Assert.Equal("?pagina=1&tamanhoPagina=25", contexto.Handler.RequestUri?.Query);
    }

    private static ContextoTeste CriarContexto()
    {
        var handler = new CapturaRequestHandler();
        var http = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.detara.test/")
        };
        var servico = new PlataformaServico(
            new HttpClientPlataforma(http),
            new PlatformTokenStorage(new JsRuntimeNulo()));
        return new ContextoTeste(http, handler, servico);
    }

    private sealed record ContextoTeste(
        HttpClient Http,
        CapturaRequestHandler Handler,
        PlataformaServico Servico) : IDisposable
    {
        public void Dispose() => Http.Dispose();
    }

    private sealed class CapturaRequestHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(RespostaApi<PaginaResponse<EmpresaPlataformaResumoResponse>>.Ok(
                    new PaginaResponse<EmpresaPlataformaResumoResponse>([], 1, 25, 0, 0)))
            });
        }
    }

    private sealed class JsRuntimeNulo : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            ValueTask.FromResult(default(TValue)!);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args) => ValueTask.FromResult(default(TValue)!);
    }
}
