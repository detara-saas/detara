using System.Net.Http.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Financeiro;

namespace Detara.Web.Servicos;

public sealed class DespesasServico(HttpClient http)
{
    private const string Base = "api/financeiro/despesas";
    public Task<ResultadoServico<T>> ObterAsync<T>(string caminho = "") => EnviarAsync<T>(() => http.GetAsync(Base + caminho));
    public Task<ResultadoServico<DespesaIdResponse>> SalvarAsync<T>(string caminho, T dados, bool editar = false) =>
        EnviarAsync<DespesaIdResponse>(() => editar ? http.PutAsJsonAsync(Base + caminho, dados) : http.PostAsJsonAsync(Base + caminho, dados));
    private static async Task<ResultadoServico<T>> EnviarAsync<T>(Func<Task<HttpResponseMessage>> enviar)
    {
        try
        {
            using var resposta = await enviar();
            var envelope = await resposta.Content.ReadFromJsonAsync<RespostaApi<T>>();
            return resposta.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
                ? ResultadoServico<T>.Ok(envelope.Resultado, envelope.Info)
                : ResultadoServico<T>.Falha(envelope?.Info ?? "Não foi possível concluir a operação.");
        }
        catch (HttpRequestException) { return ResultadoServico<T>.Falha("Não foi possível acessar a API."); }
        catch (TaskCanceledException) { return ResultadoServico<T>.Falha("A solicitação demorou mais que o esperado. Atualize a página para conferir o resultado antes de tentar novamente."); }
        catch (System.Text.Json.JsonException) { return ResultadoServico<T>.Falha("A API retornou uma resposta inválida. Atualize a página antes de tentar novamente."); }
    }
}
