using System.Globalization;
using System.Net.Http.Json;
using Detara.Contracts.Agenda;
using Detara.Contracts.Comum;

namespace Detara.Web.Servicos;

public sealed class AgendaServico(HttpClient http)
{
    public Task<ResultadoServico<ContextoAgendaResponse>> ObterContextoAsync(CancellationToken ct = default) => ObterAsync<ContextoAgendaResponse>("api/agenda/contexto", ct);

    public Task<ResultadoServico<IReadOnlyCollection<AgendamentoPeriodoResponse>>> ListarPeriodoAsync(DateTime inicioUtc, DateTime fimUtc, StatusAgendamentoContrato? status = null, string? pesquisa = null, CancellationToken ct = default)
    {
        var parametros = new List<string> { $"inicioUtc={Uri.EscapeDataString(inicioUtc.ToString("O", CultureInfo.InvariantCulture))}", $"fimUtc={Uri.EscapeDataString(fimUtc.ToString("O", CultureInfo.InvariantCulture))}" };
        if (status.HasValue) parametros.Add($"status={(int)status.Value}");
        if (!string.IsNullOrWhiteSpace(pesquisa)) parametros.Add($"pesquisa={Uri.EscapeDataString(pesquisa)}");
        return ObterAsync<IReadOnlyCollection<AgendamentoPeriodoResponse>>($"api/agenda?{string.Join('&', parametros)}", ct);
    }

    public Task<ResultadoServico<PaginaResponse<AgendamentoListaResponse>>> ListarHistoricoAsync(int pagina, int tamanhoPagina, string? pesquisa, StatusAgendamentoContrato? status, CancellationToken ct = default)
    {
        var parametros = new List<string> { $"pagina={pagina}", $"tamanhoPagina={tamanhoPagina}" };
        if (!string.IsNullOrWhiteSpace(pesquisa)) parametros.Add($"pesquisa={Uri.EscapeDataString(pesquisa)}");
        if (status.HasValue) parametros.Add($"status={(int)status.Value}");
        return ObterAsync<PaginaResponse<AgendamentoListaResponse>>($"api/agendamentos?{string.Join('&', parametros)}", ct);
    }

    public Task<ResultadoServico<AgendamentoDetalheResponse>> ObterAsync(Guid id, CancellationToken ct = default) => ObterAsync<AgendamentoDetalheResponse>($"api/agendamentos/{id}", ct);
    public Task<ResultadoServico<AgendamentoDetalheResponse>> CriarAsync(SalvarAgendamentoRequest request, CancellationToken ct = default) => EnviarAsync<AgendamentoDetalheResponse>(() => http.PostAsJsonAsync("api/agendamentos", request, ct), ct);
    public Task<ResultadoServico<AgendamentoDetalheResponse>> AtualizarAsync(Guid id, SalvarAgendamentoRequest request, CancellationToken ct = default) => EnviarAsync<AgendamentoDetalheResponse>(() => http.PutAsJsonAsync($"api/agendamentos/{id}", request, ct), ct);
    public Task<ResultadoServico<AgendamentoDetalheResponse>> ReagendarAsync(Guid id, ReagendarAgendamentoRequest request, CancellationToken ct = default) => EnviarAsync<AgendamentoDetalheResponse>(() => http.PatchAsJsonAsync($"api/agendamentos/{id}/reagendar", request, ct), ct);
    public Task<ResultadoServico<AgendamentoDetalheResponse>> AlterarStatusAsync(Guid id, AlterarStatusAgendamentoRequest request, CancellationToken ct = default) => EnviarAsync<AgendamentoDetalheResponse>(() => http.PatchAsJsonAsync($"api/agendamentos/{id}/status", request, ct), ct);
    public Task<ResultadoServico<IReadOnlyCollection<ClienteAgendaResponse>>> BuscarClientesAsync(string pesquisa, CancellationToken ct = default) => ObterAsync<IReadOnlyCollection<ClienteAgendaResponse>>($"api/agenda/clientes?pesquisa={Uri.EscapeDataString(pesquisa)}", ct);
    public Task<ResultadoServico<IReadOnlyCollection<VeiculoAgendaResponse>>> ListarVeiculosAsync(Guid clienteId, bool incluirInativos = false, CancellationToken ct = default) => ObterAsync<IReadOnlyCollection<VeiculoAgendaResponse>>($"api/agenda/clientes/{clienteId}/veiculos?incluirInativos={incluirInativos.ToString().ToLowerInvariant()}", ct);
    public Task<ResultadoServico<IReadOnlyCollection<ItemCatalogoAgendaResponse>>> BuscarCatalogoAsync(string? pesquisa, bool incluirInativos = false, CancellationToken ct = default) => ObterAsync<IReadOnlyCollection<ItemCatalogoAgendaResponse>>($"api/agenda/catalogo?pesquisa={Uri.EscapeDataString(pesquisa ?? string.Empty)}&incluirInativos={incluirInativos.ToString().ToLowerInvariant()}", ct);
    public Task<ResultadoServico<int>> ContarSobreposicoesAsync(DateTime inicioLocal, int duracao, Guid? ignorarId, CancellationToken ct = default) => ObterAsync<int>($"api/agenda/sobreposicoes?inicioLocal={Uri.EscapeDataString(inicioLocal.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture))}&duracaoPlanejadaMinutos={duracao}{(ignorarId.HasValue ? $"&ignorarAgendamentoId={ignorarId}" : string.Empty)}", ct);

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
