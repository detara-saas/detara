using System.Net.Http.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Onboarding;

namespace Detara.Web.Servicos;

public sealed class OnboardingServico(HttpClient httpClient)
{
    public async Task<ResultadoServico<OnboardingEmpresaResponse>> ObterAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await httpClient.GetAsync("api/onboarding", cancellationToken);
            var envelope = await response.Content
                .ReadFromJsonAsync<RespostaApi<OnboardingEmpresaResponse>>(cancellationToken);
            return response.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
                ? ResultadoServico<OnboardingEmpresaResponse>.Ok(envelope.Resultado)
                : ResultadoServico<OnboardingEmpresaResponse>.Falha(
                    envelope?.Info ?? "Não foi possível carregar a configuração inicial.");
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<OnboardingEmpresaResponse>.Falha(
                "A configuração inicial não está disponível no momento.");
        }
    }
}
