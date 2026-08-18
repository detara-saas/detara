using System.Net.Http.Json;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Comum;

namespace Detara.Web.Servicos;

public sealed class ConfiguracoesServico(HttpClient httpClient)
{
    public Task<ResultadoServico<ConfiguracaoOperacionalResponse>> ObterOperacaoAsync(
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            () => httpClient.GetAsync("api/configuracoes/operacao", cancellationToken),
            cancellationToken);

    public Task<ResultadoServico<ConfiguracaoOperacionalResponse>> AtualizarOperacaoAsync(
        AtualizarConfiguracaoOperacionalRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            () => httpClient.PutAsJsonAsync("api/configuracoes/operacao", request, cancellationToken),
            cancellationToken);

    public Task<ResultadoServico<ConfiguracaoOperacionalResponse>> AtualizarChecklistAsync(
        AtualizarChecklistModeloRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync(
            () => httpClient.PutAsJsonAsync("api/configuracoes/operacao/checklist", request, cancellationToken),
            cancellationToken);

    private static async Task<ResultadoServico<ConfiguracaoOperacionalResponse>> EnviarAsync(
        Func<Task<HttpResponseMessage>> enviar,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await enviar();
            var envelope = await response.Content
                .ReadFromJsonAsync<RespostaApi<ConfiguracaoOperacionalResponse>>(cancellationToken);
            return response.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
                ? ResultadoServico<ConfiguracaoOperacionalResponse>.Ok(envelope.Resultado, envelope.Info)
                : ResultadoServico<ConfiguracaoOperacionalResponse>.Falha(
                    envelope?.Info ?? "Não foi possível concluir a operação.");
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<ConfiguracaoOperacionalResponse>.Falha(
                "Não foi possível acessar a API.");
        }
    }
}
