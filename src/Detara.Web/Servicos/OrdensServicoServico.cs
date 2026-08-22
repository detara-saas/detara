using System.Net.Http.Headers;
using System.Net.Http.Json;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Comum;
using Microsoft.AspNetCore.Components.Forms;

namespace Detara.Web.Servicos;

public sealed class OrdensServicoServico(HttpClient http)
{
    public sealed record ConteudoFoto(byte[] Bytes, string ContentType);
    public Task<ResultadoServico<PaginaResponse<OrdemServicoListaResponse>>> ListarAsync(int pagina, int tamanhoPagina,
        string? pesquisa, StatusOrdemServicoContrato? status, DateOnly? inicio = null, DateOnly? fim = null,
        CancellationToken ct = default)
    {
        var parametros = new List<string> { $"pagina={pagina}", $"tamanhoPagina={tamanhoPagina}" };
        if (!string.IsNullOrWhiteSpace(pesquisa)) parametros.Add($"pesquisa={Uri.EscapeDataString(pesquisa)}");
        if (status.HasValue) parametros.Add($"status={(int)status.Value}");
        if (inicio.HasValue) parametros.Add($"dataInicial={inicio:yyyy-MM-dd}");
        if (fim.HasValue) parametros.Add($"dataFinal={fim:yyyy-MM-dd}");
        return ObterAsync<PaginaResponse<OrdemServicoListaResponse>>($"api/ordens-servico?{string.Join('&', parametros)}", ct);
    }

    public Task<ResultadoServico<OrdemServicoDetalheResponse>> ObterAsync(Guid id, CancellationToken ct = default) =>
        ObterAsync<OrdemServicoDetalheResponse>($"api/ordens-servico/{id}", ct);
    public Task<ResultadoServico<VinculoOrdemServicoAgendamentoResponse>> ObterPorAgendamentoAsync(Guid agendamentoId,
        CancellationToken ct = default) =>
        ObterAsync<VinculoOrdemServicoAgendamentoResponse>($"api/ordens-servico/agendamentos/{agendamentoId}", ct);
    public Task<ResultadoServico<OrdemServicoDetalheResponse>> CriarAsync(CriarOrdemServicoRequest request, CancellationToken ct = default) =>
        EnviarAsync<OrdemServicoDetalheResponse>(() => http.PostAsJsonAsync("api/ordens-servico", request, ct), ct);
    public Task<ResultadoServico<OrdemServicoDetalheResponse>> CheckInAsync(Guid id, RealizarCheckInRequest request, CancellationToken ct = default) =>
        EnviarAsync<OrdemServicoDetalheResponse>(() => http.PostAsJsonAsync($"api/ordens-servico/{id}/check-in", request, ct), ct);
    public Task<ResultadoServico<OrdemServicoDetalheResponse>> AtualizarChecklistAsync(Guid id,
        AtualizarChecklistOrdemServicoRequest request, CancellationToken ct = default) =>
        EnviarAsync<OrdemServicoDetalheResponse>(() => http.PutAsJsonAsync($"api/ordens-servico/{id}/checklist", request, ct), ct);
    public Task<ResultadoServico<OrdemServicoDetalheResponse>> IniciarAsync(Guid id, string? observacao, CancellationToken ct = default) =>
        TransicaoAsync(id, "iniciar-execucao", observacao, ct);
    public Task<ResultadoServico<OrdemServicoDetalheResponse>> FinalizarAsync(Guid id, string? observacao, CancellationToken ct = default) =>
        TransicaoAsync(id, "finalizar-execucao", observacao, ct);
    public Task<ResultadoServico<OrdemServicoDetalheResponse>> ConcluirAsync(Guid id, string? observacao, CancellationToken ct = default) =>
        TransicaoAsync(id, "concluir", observacao, ct);
    public Task<ResultadoServico<OrdemServicoDetalheResponse>> CancelarAsync(Guid id, string motivo, CancellationToken ct = default) =>
        EnviarAsync<OrdemServicoDetalheResponse>(() => http.PostAsJsonAsync($"api/ordens-servico/{id}/cancelar", new CancelarOrdemServicoRequest(motivo), ct), ct);
    public Task<ResultadoServico<OrcamentoDetalheResponse>> CriarOrcamentoAdicionalAsync(Guid id,
        CriarOrcamentoAdicionalRequest request, CancellationToken ct = default) =>
        EnviarAsync<OrcamentoDetalheResponse>(() => http.PostAsJsonAsync($"api/ordens-servico/{id}/orcamento-adicional", request, ct), ct);

    public async Task<ResultadoServico<OrdemServicoFotoResponse>> EnviarFotoAsync(Guid id,
        CategoriaFotoOrdemServicoContrato categoria, IBrowserFile arquivo, CancellationToken ct = default)
    {
        try
        {
            using var form = new MultipartFormDataContent();
            form.Add(new StringContent(((int)categoria).ToString()), "categoria");
            var stream = arquivo.OpenReadStream(10L * 1024 * 1024, ct);
            var conteudo = new StreamContent(stream);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue(arquivo.ContentType);
            form.Add(conteudo, "arquivo", arquivo.Name);
            return await ConverterAsync<OrdemServicoFotoResponse>(await http.PostAsync($"api/ordens-servico/{id}/fotos", form, ct), ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or ArgumentException)
        { return ResultadoServico<OrdemServicoFotoResponse>.Falha("Não foi possível enviar a foto."); }
    }

    public async Task<ResultadoServico<bool>> ExcluirFotoAsync(Guid id, Guid fotoId, CancellationToken ct = default)
    {
        try
        {
            var response = await http.DeleteAsync($"api/ordens-servico/{id}/fotos/{fotoId}", ct);
            return response.IsSuccessStatusCode ? ResultadoServico<bool>.Ok(true, "Foto excluída.")
                : ResultadoServico<bool>.Falha("Não foi possível excluir a foto.");
        }
        catch (HttpRequestException) { return ResultadoServico<bool>.Falha("Não foi possível acessar a API."); }
    }
    public string UrlFoto(Guid id, Guid fotoId) => new Uri(http.BaseAddress!, $"api/ordens-servico/{id}/fotos/{fotoId}").ToString();
    public async Task<ResultadoServico<ConteudoFoto>> ObterFotoAsync(Guid id, Guid fotoId, CancellationToken ct = default)
    {
        try
        {
            var response = await http.GetAsync($"api/ordens-servico/{id}/fotos/{fotoId}", ct);
            return response.IsSuccessStatusCode
                ? ResultadoServico<ConteudoFoto>.Ok(new(await response.Content.ReadAsByteArrayAsync(ct),
                    response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"))
                : ResultadoServico<ConteudoFoto>.Falha("Não foi possível carregar a foto.");
        }
        catch (HttpRequestException) { return ResultadoServico<ConteudoFoto>.Falha("Não foi possível acessar a API."); }
    }

    private Task<ResultadoServico<OrdemServicoDetalheResponse>> TransicaoAsync(Guid id, string acao,
        string? observacao, CancellationToken ct) => EnviarAsync<OrdemServicoDetalheResponse>(() =>
            http.PostAsJsonAsync($"api/ordens-servico/{id}/{acao}", new TransicaoOrdemServicoRequest(observacao), ct), ct);
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
