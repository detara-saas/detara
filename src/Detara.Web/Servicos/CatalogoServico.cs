using System.Net.Http.Json;
using Detara.Contracts.Catalogo;
using Detara.Contracts.Comum;

namespace Detara.Web.Servicos;

public sealed class CatalogoServico(HttpClient http)
{
    public Task<ResultadoServico<IReadOnlyCollection<CategoriaServicoResponse>>> ListarCategoriasAsync(bool? ativo = null, CancellationToken ct = default) =>
        ObterAsync<IReadOnlyCollection<CategoriaServicoResponse>>($"api/categorias-servico{(ativo.HasValue ? $"?ehAtivo={ativo.Value.ToString().ToLowerInvariant()}" : string.Empty)}", ct);
    public Task<ResultadoServico<CategoriaServicoResponse>> CriarCategoriaAsync(SalvarCategoriaServicoRequest request, CancellationToken ct = default) => EnviarAsync<CategoriaServicoResponse>(() => http.PostAsJsonAsync("api/categorias-servico", request, ct), ct);
    public Task<ResultadoServico<CategoriaServicoResponse>> AtualizarCategoriaAsync(Guid id, SalvarCategoriaServicoRequest request, CancellationToken ct = default) => EnviarAsync<CategoriaServicoResponse>(() => http.PutAsJsonAsync($"api/categorias-servico/{id}", request, ct), ct);
    public Task<ResultadoServico<bool>> AlterarStatusCategoriaAsync(Guid id, bool ativo, CancellationToken ct = default) => AlterarStatusAsync($"api/categorias-servico/{id}/status", ativo, "Categoria", ct);

    public Task<ResultadoServico<PaginaResponse<ServicoListaResponse>>> ListarServicosAsync(int pagina, string? pesquisa, bool? ativo, Guid? categoriaId, CancellationToken ct = default)
    {
        var p = new List<string> { $"pagina={pagina}", "tamanhoPagina=25" }; Adicionar(p, "pesquisa", pesquisa); Adicionar(p, "ehAtivo", ativo?.ToString().ToLowerInvariant()); Adicionar(p, "categoriaServicoId", categoriaId?.ToString());
        return ObterAsync<PaginaResponse<ServicoListaResponse>>($"api/servicos?{string.Join('&', p)}", ct);
    }
    public Task<ResultadoServico<IReadOnlyCollection<ServicoSelecaoResponse>>> ListarServicosSelecaoAsync(bool incluirInativos = false, CancellationToken ct = default) => ObterAsync<IReadOnlyCollection<ServicoSelecaoResponse>>($"api/servicos/selecao?incluirInativos={incluirInativos.ToString().ToLowerInvariant()}", ct);
    public Task<ResultadoServico<ServicoDetalheResponse>> ObterServicoAsync(Guid id, CancellationToken ct = default) => ObterAsync<ServicoDetalheResponse>($"api/servicos/{id}", ct);
    public Task<ResultadoServico<ServicoDetalheResponse>> CriarServicoAsync(SalvarServicoRequest request, CancellationToken ct = default) => EnviarAsync<ServicoDetalheResponse>(() => http.PostAsJsonAsync("api/servicos", request, ct), ct);
    public Task<ResultadoServico<ServicoDetalheResponse>> AtualizarServicoAsync(Guid id, SalvarServicoRequest request, CancellationToken ct = default) => EnviarAsync<ServicoDetalheResponse>(() => http.PutAsJsonAsync($"api/servicos/{id}", request, ct), ct);
    public Task<ResultadoServico<bool>> AlterarStatusServicoAsync(Guid id, bool ativo, CancellationToken ct = default) => AlterarStatusAsync($"api/servicos/{id}/status", ativo, "Serviço", ct);

    public Task<ResultadoServico<PaginaResponse<PacoteListaResponse>>> ListarPacotesAsync(int pagina, string? pesquisa, bool? ativo, CancellationToken ct = default)
    {
        var p = new List<string> { $"pagina={pagina}", "tamanhoPagina=25" }; Adicionar(p, "pesquisa", pesquisa); Adicionar(p, "ehAtivo", ativo?.ToString().ToLowerInvariant());
        return ObterAsync<PaginaResponse<PacoteListaResponse>>($"api/pacotes?{string.Join('&', p)}", ct);
    }
    public Task<ResultadoServico<PacoteDetalheResponse>> ObterPacoteAsync(Guid id, CancellationToken ct = default) => ObterAsync<PacoteDetalheResponse>($"api/pacotes/{id}", ct);
    public Task<ResultadoServico<PacoteDetalheResponse>> CriarPacoteAsync(SalvarPacoteRequest request, CancellationToken ct = default) => EnviarAsync<PacoteDetalheResponse>(() => http.PostAsJsonAsync("api/pacotes", request, ct), ct);
    public Task<ResultadoServico<PacoteDetalheResponse>> AtualizarPacoteAsync(Guid id, SalvarPacoteRequest request, CancellationToken ct = default) => EnviarAsync<PacoteDetalheResponse>(() => http.PutAsJsonAsync($"api/pacotes/{id}", request, ct), ct);
    public Task<ResultadoServico<bool>> AlterarStatusPacoteAsync(Guid id, bool ativo, CancellationToken ct = default) => AlterarStatusAsync($"api/pacotes/{id}/status", ativo, "Pacote", ct);

    private async Task<ResultadoServico<bool>> AlterarStatusAsync(string url, bool ativo, string entidade, CancellationToken ct)
    {
        try { var response = await http.PatchAsJsonAsync(url, new AlterarStatusRequest(ativo), ct); return response.IsSuccessStatusCode ? ResultadoServico<bool>.Ok(true, $"{entidade} {(ativo ? "ativado" : "inativado")}.") : ResultadoServico<bool>.Falha(await LerMensagemAsync(response, ct)); }
        catch (HttpRequestException) { return ResultadoServico<bool>.Falha("Não foi possível acessar a API."); }
    }
    private async Task<ResultadoServico<T>> ObterAsync<T>(string url, CancellationToken ct) { try { return await ConverterAsync<T>(await http.GetAsync(url, ct), ct); } catch (HttpRequestException) { return ResultadoServico<T>.Falha("Não foi possível acessar a API."); } }
    private async Task<ResultadoServico<T>> EnviarAsync<T>(Func<Task<HttpResponseMessage>> enviar, CancellationToken ct) { try { return await ConverterAsync<T>(await enviar(), ct); } catch (HttpRequestException) { return ResultadoServico<T>.Falha("Não foi possível acessar a API."); } }
    private static async Task<ResultadoServico<T>> ConverterAsync<T>(HttpResponseMessage response, CancellationToken ct) { var e = await response.Content.ReadFromJsonAsync<RespostaApi<T>>(ct); return response.IsSuccessStatusCode && e is { Sucesso: true, Resultado: not null } ? ResultadoServico<T>.Ok(e.Resultado, e.Info) : ResultadoServico<T>.Falha(e?.Info ?? "Não foi possível concluir a operação."); }
    private static async Task<string> LerMensagemAsync(HttpResponseMessage response, CancellationToken ct) => (await response.Content.ReadFromJsonAsync<RespostaApi<object>>(ct))?.Info ?? "Não foi possível concluir a operação.";
    private static void Adicionar(ICollection<string> p, string n, string? v) { if (!string.IsNullOrWhiteSpace(v)) p.Add($"{n}={Uri.EscapeDataString(v)}"); }
}
