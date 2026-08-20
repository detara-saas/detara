using System.Net.Http.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Financeiro;

namespace Detara.Web.Servicos;

public sealed class FinanceiroServico(HttpClient http)
{
    public Task<ResultadoServico<ResumoFinanceiroResponse>> ObterResumoAsync(DateOnly? inicio = null,
        DateOnly? fim = null, CancellationToken ct = default)
    {
        var parametros = new List<string>();
        if (inicio.HasValue) parametros.Add($"inicio={inicio:yyyy-MM-dd}");
        if (fim.HasValue) parametros.Add($"fim={fim:yyyy-MM-dd}");
        return ObterAsync<ResumoFinanceiroResponse>($"api/financeiro/resumo{(parametros.Count > 0 ? $"?{string.Join('&', parametros)}" : string.Empty)}", ct);
    }

    public Task<ResultadoServico<PaginaResponse<ContaReceberListaResponse>>> ListarAsync(int pagina,
        int tamanhoPagina, string? pesquisa, StatusContaReceberContrato? status, bool? vencida,
        DateOnly? inicio = null, DateOnly? fim = null, CancellationToken ct = default)
    {
        var parametros = new List<string> { $"pagina={pagina}", $"tamanhoPagina={tamanhoPagina}" };
        if (!string.IsNullOrWhiteSpace(pesquisa)) parametros.Add($"pesquisa={Uri.EscapeDataString(pesquisa)}");
        if (status.HasValue) parametros.Add($"status={(int)status.Value}");
        if (vencida.HasValue) parametros.Add($"vencida={vencida.Value.ToString().ToLowerInvariant()}");
        if (inicio.HasValue) parametros.Add($"competenciaInicial={inicio:yyyy-MM-dd}");
        if (fim.HasValue) parametros.Add($"competenciaFinal={fim:yyyy-MM-dd}");
        return ObterAsync<PaginaResponse<ContaReceberListaResponse>>(
            $"api/financeiro/contas-receber?{string.Join('&', parametros)}", ct);
    }

    public Task<ResultadoServico<ContaReceberDetalheResponse>> ObterAsync(Guid id, CancellationToken ct = default) =>
        ObterAsync<ContaReceberDetalheResponse>($"api/financeiro/contas-receber/{id}", ct);

    public Task<ResultadoServico<ContaReceberVinculoResponse>> ObterPorOrdemServicoAsync(Guid ordemServicoId,
        CancellationToken ct = default) => ObterAsync<ContaReceberVinculoResponse>(
            $"api/financeiro/contas-receber/por-ordem-servico/{ordemServicoId}", ct);

    public Task<ResultadoServico<ContaReceberDetalheResponse>> RegistrarPagamentoAsync(Guid id,
        RegistrarPagamentoRequest request, CancellationToken ct = default) => EnviarAsync<ContaReceberDetalheResponse>(() =>
            http.PostAsJsonAsync($"api/financeiro/contas-receber/{id}/pagamentos", request, ct), ct);

    public Task<ResultadoServico<ContaReceberDetalheResponse>> EstornarPagamentoAsync(Guid id,
        Guid pagamentoId, EstornarPagamentoRequest request, CancellationToken ct = default) =>
        EnviarAsync<ContaReceberDetalheResponse>(() => http.PostAsJsonAsync(
            $"api/financeiro/contas-receber/{id}/pagamentos/{pagamentoId}/estornar", request, ct), ct);

    public Task<ResultadoServico<ContaReceberDetalheResponse>> AlterarVencimentoAsync(Guid id,
        AlterarVencimentoRequest request, CancellationToken ct = default) => EnviarAsync<ContaReceberDetalheResponse>(() =>
        {
            var message = new HttpRequestMessage(HttpMethod.Patch, $"api/financeiro/contas-receber/{id}/vencimento")
            { Content = JsonContent.Create(request) };
            return http.SendAsync(message, ct);
        }, ct);

    private async Task<ResultadoServico<T>> ObterAsync<T>(string endereco, CancellationToken ct)
    { try { return await ConverterAsync<T>(await http.GetAsync(endereco, ct), ct); } catch (HttpRequestException) { return ResultadoServico<T>.Falha("Não foi possível acessar a API."); } }
    private async Task<ResultadoServico<T>> EnviarAsync<T>(Func<Task<HttpResponseMessage>> enviar, CancellationToken ct)
    { try { return await ConverterAsync<T>(await enviar(), ct); } catch (HttpRequestException) { return ResultadoServico<T>.Falha("Não foi possível acessar a API."); } }
    private static async Task<ResultadoServico<T>> ConverterAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        var envelope = await response.Content.ReadFromJsonAsync<RespostaApi<T>>(ct);
        return response.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
            ? ResultadoServico<T>.Ok(envelope.Resultado, envelope.Info)
            : ResultadoServico<T>.Falha(envelope?.Info ?? "Não foi possível concluir a operação.");
    }
}
