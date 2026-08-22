using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Dashboard;

namespace Detara.Web.Servicos;

public sealed class DashboardServico(HttpClient httpClient)
{
    public async Task<ResultadoServico<DashboardOperacionalResponse>> ObterAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("api/dashboard", cancellationToken);
            if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                return ResultadoServico<DashboardOperacionalResponse>.Falha(
                    "Seu perfil não possui acesso aos dados operacionais do Dashboard.");
            }
            if (response.StatusCode == HttpStatusCode.Unauthorized ||
                response.Content.Headers.ContentLength == 0)
            {
                return ResultadoServico<DashboardOperacionalResponse>.Falha(
                    "Sua sessão não está disponível. Entre novamente para continuar.");
            }

            var envelope = await response.Content
                .ReadFromJsonAsync<RespostaApi<DashboardOperacionalResponse>>(cancellationToken);
            return response.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
                ? ResultadoServico<DashboardOperacionalResponse>.Ok(envelope.Resultado)
                : ResultadoServico<DashboardOperacionalResponse>.Falha(
                    envelope?.Info ?? "Não foi possível carregar o Dashboard.");
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<DashboardOperacionalResponse>.Falha(
                "O Dashboard não está disponível no momento.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoServico<DashboardOperacionalResponse>.Falha(
                "A API não respondeu dentro do tempo esperado.");
        }
        catch (JsonException)
        {
            return ResultadoServico<DashboardOperacionalResponse>.Falha(
                "A API retornou uma resposta inválida para o Dashboard.");
        }
    }
}
