using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Relatorios;

namespace Detara.Web.Servicos;

public sealed class RelatoriosServico(HttpClient http)
{
    public async Task<ResultadoServico<RelatorioResponse>> ObterAsync(PerspectivaRelatorioContrato perspectiva,
        PeriodoRelatorioContrato periodo, DateTime? inicio, DateTime? fim, CancellationToken ct)
    {
        var url = $"api/relatorios/{(int)perspectiva}?periodo={(int)periodo}";
        if (periodo == PeriodoRelatorioContrato.Personalizado)
            url += $"&inicio={inicio:yyyy-MM-dd}&fim={fim:yyyy-MM-dd}";
        try
        {
            using var resposta = await http.GetAsync(url, ct);
            if (resposta.StatusCode == HttpStatusCode.Forbidden) return ResultadoServico<RelatorioResponse>.Falha("Seu perfil não possui acesso a esta perspectiva.");
            if (resposta.StatusCode == HttpStatusCode.Unauthorized) return ResultadoServico<RelatorioResponse>.Falha("Entre novamente para consultar os relatórios.");
            var envelope = await resposta.Content.ReadFromJsonAsync<RespostaApi<RelatorioResponse>>(ct);
            return resposta.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
                ? ResultadoServico<RelatorioResponse>.Ok(envelope.Resultado)
                : ResultadoServico<RelatorioResponse>.Falha(envelope?.Info ?? "Não foi possível carregar o relatório.");
        }
        catch (HttpRequestException) { return ResultadoServico<RelatorioResponse>.Falha("A API não está disponível. Tente novamente."); }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested) { return ResultadoServico<RelatorioResponse>.Falha("A consulta demorou mais que o esperado. Tente novamente."); }
        catch (JsonException) { return ResultadoServico<RelatorioResponse>.Falha("A API retornou uma resposta inválida para o relatório."); }
    }
}
