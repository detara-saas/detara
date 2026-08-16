using System.Net.Http.Json;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Comum;

namespace Detara.Web.Servicos;

public sealed class OrcamentosServico(HttpClient http)
{
    public Task<ResultadoServico<PaginaResponse<OrcamentoListaResponse>>> ListarAsync(int pagina, int tamanhoPagina, string? pesquisa,
        StatusOrcamentoContrato? status, CancellationToken ct = default)
    {
        var parametros = new List<string> { $"pagina={pagina}", $"tamanhoPagina={tamanhoPagina}" };
        if (!string.IsNullOrWhiteSpace(pesquisa)) parametros.Add($"pesquisa={Uri.EscapeDataString(pesquisa)}");
        if (status.HasValue) parametros.Add($"status={(int)status.Value}");
        return ObterAsync<PaginaResponse<OrcamentoListaResponse>>($"api/orcamentos?{string.Join('&', parametros)}", ct);
    }

    public Task<ResultadoServico<ContextoOrcamentoResponse>> ObterContextoAsync(CancellationToken ct = default) => ObterAsync<ContextoOrcamentoResponse>("api/orcamentos/contexto", ct);
    public Task<ResultadoServico<OrcamentoDetalheResponse>> ObterAsync(Guid id, CancellationToken ct = default) => ObterAsync<OrcamentoDetalheResponse>($"api/orcamentos/{id}", ct);
    public Task<ResultadoServico<OrcamentoDetalheResponse>> CriarAsync(SalvarOrcamentoRequest request, CancellationToken ct = default) => EnviarAsync<OrcamentoDetalheResponse>(() => http.PostAsJsonAsync("api/orcamentos", request, ct), ct);
    public Task<ResultadoServico<OrcamentoDetalheResponse>> AtualizarAsync(Guid id, SalvarOrcamentoRequest request, CancellationToken ct = default) => EnviarAsync<OrcamentoDetalheResponse>(() => http.PutAsJsonAsync($"api/orcamentos/{id}", request, ct), ct);
    public Task<ResultadoServico<OrcamentoDetalheResponse>> EmitirAsync(Guid id, string? observacao, CancellationToken ct = default) => TransicaoAsync(id, "emitir", observacao, ct);
    public Task<ResultadoServico<OrcamentoDetalheResponse>> AprovarAsync(Guid id, string? observacao, CancellationToken ct = default) => TransicaoAsync(id, "aprovar", observacao, ct);
    public Task<ResultadoServico<OrcamentoDetalheResponse>> RecusarAsync(Guid id, string? observacao, CancellationToken ct = default) => TransicaoAsync(id, "recusar", observacao, ct);
    public Task<ResultadoServico<OrcamentoDetalheResponse>> CancelarAsync(Guid id, string? observacao, CancellationToken ct = default) => TransicaoAsync(id, "cancelar", observacao, ct);
    public Task<ResultadoServico<OrcamentoDetalheResponse>> NovaPropostaAsync(Guid id, CancellationToken ct = default) => EnviarAsync<OrcamentoDetalheResponse>(() => http.PostAsync($"api/orcamentos/{id}/nova-proposta", null, ct), ct);
    public Task<ResultadoServico<IReadOnlyCollection<ClienteOrcamentoResponse>>> BuscarClientesAsync(string pesquisa, CancellationToken ct = default) => ObterAsync<IReadOnlyCollection<ClienteOrcamentoResponse>>($"api/orcamentos/clientes?pesquisa={Uri.EscapeDataString(pesquisa)}", ct);
    public Task<ResultadoServico<IReadOnlyCollection<VeiculoOrcamentoResponse>>> ListarVeiculosAsync(Guid clienteId, CancellationToken ct = default) => ObterAsync<IReadOnlyCollection<VeiculoOrcamentoResponse>>($"api/orcamentos/clientes/{clienteId}/veiculos", ct);
    public Task<ResultadoServico<IReadOnlyCollection<ItemCatalogoOrcamentoResponse>>> BuscarCatalogoAsync(string? pesquisa, CancellationToken ct = default) => ObterAsync<IReadOnlyCollection<ItemCatalogoOrcamentoResponse>>($"api/orcamentos/catalogo?pesquisa={Uri.EscapeDataString(pesquisa ?? string.Empty)}", ct);
    public Task<ResultadoServico<OrigemAgendamentoOrcamentoResponse>> ObterOrigemAsync(Guid agendamentoId, CancellationToken ct = default) => ObterAsync<OrigemAgendamentoOrcamentoResponse>($"api/orcamentos/agendamentos/{agendamentoId}/origem", ct);

    public async Task<(byte[]? Conteudo, string Mensagem)> BaixarPdfAsync(Guid id, CancellationToken ct = default)
    {
        try { var response = await http.GetAsync($"api/orcamentos/{id}/pdf", ct); return response.IsSuccessStatusCode ? (await response.Content.ReadAsByteArrayAsync(ct), string.Empty) : (null, "Não foi possível gerar o PDF oficial."); }
        catch (HttpRequestException) { return (null, "Não foi possível acessar a API."); }
    }

    private Task<ResultadoServico<OrcamentoDetalheResponse>> TransicaoAsync(Guid id, string acao, string? observacao, CancellationToken ct) =>
        EnviarAsync<OrcamentoDetalheResponse>(() => http.PostAsJsonAsync($"api/orcamentos/{id}/{acao}", new RegistrarTransicaoOrcamentoRequest(observacao), ct), ct);
    private async Task<ResultadoServico<T>> ObterAsync<T>(string endereco, CancellationToken ct)
    { try { return await ConverterAsync<T>(await http.GetAsync(endereco, ct), ct); } catch (HttpRequestException) { return ResultadoServico<T>.Falha("Não foi possível acessar a API."); } }
    private async Task<ResultadoServico<T>> EnviarAsync<T>(Func<Task<HttpResponseMessage>> enviar, CancellationToken ct)
    { try { return await ConverterAsync<T>(await enviar(), ct); } catch (HttpRequestException) { return ResultadoServico<T>.Falha("Não foi possível acessar a API."); } }
    private static async Task<ResultadoServico<T>> ConverterAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var envelope = await response.Content.ReadFromJsonAsync<RespostaApi<T>>(ct);
        return response.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
            ? ResultadoServico<T>.Ok(envelope.Resultado, envelope.Info) : ResultadoServico<T>.Falha(envelope?.Info ?? "Não foi possível concluir a operação.");
    }
}
