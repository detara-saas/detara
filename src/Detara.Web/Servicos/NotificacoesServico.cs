using System.Net.Http.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Notificacoes;

namespace Detara.Web.Servicos;

public sealed class NotificacoesServico(HttpClient http)
{
    public Task<ResultadoServico<ConfiguracaoNotificacaoResponse>> ObterConfiguracaoAsync(CancellationToken ct = default) =>
        ObterAsync<ConfiguracaoNotificacaoResponse>("api/notificacoes/configuracao", ct);
    public Task<ResultadoServico<ConfiguracaoNotificacaoResponse>> SalvarConfiguracaoAsync(
        AtualizarConfiguracaoNotificacaoRequest request, CancellationToken ct = default) =>
        EnviarAsync<ConfiguracaoNotificacaoResponse>(() => http.PutAsJsonAsync("api/notificacoes/configuracao", request, ct), ct);
    public Task<ResultadoServico<TemplateEmailResponse>> ObterTemplateAsync(CancellationToken ct = default) =>
        ObterAsync<TemplateEmailResponse>("api/notificacoes/templates/veiculo-pronto", ct);
    public Task<ResultadoServico<TemplateEmailResponse>> SalvarTemplateAsync(SalvarTemplateEmailRequest request, CancellationToken ct = default) =>
        EnviarAsync<TemplateEmailResponse>(() => http.PutAsJsonAsync("api/notificacoes/templates/veiculo-pronto", request, ct), ct);
    public Task<ResultadoServico<TemplateEmailResponse>> RestaurarTemplateAsync(CancellationToken ct = default) =>
        EnviarAsync<TemplateEmailResponse>(() => http.DeleteAsync("api/notificacoes/templates/veiculo-pronto", ct), ct);
    public Task<ResultadoServico<PreviewTemplateEmailResponse>> PreviewAsync(PreviewTemplateEmailRequest request, CancellationToken ct = default) =>
        EnviarAsync<PreviewTemplateEmailResponse>(() => http.PostAsJsonAsync("api/notificacoes/templates/veiculo-pronto/preview", request, ct), ct);
    public Task<ResultadoServico<object>> EnviarTesteAsync(CancellationToken ct = default) =>
        EnviarAsync<object>(() => http.PostAsync("api/notificacoes/templates/veiculo-pronto/teste", null, ct), ct);
    public Task<ResultadoServico<NotificacaoOrdemServicoResponse>> ObterPorOrdemServicoAsync(Guid id, CancellationToken ct = default) =>
        ObterAsync<NotificacaoOrdemServicoResponse>($"api/notificacoes/ordens-servico/{id}", ct);
    public Task<ResultadoServico<NotificacaoEmailResponse>> EnviarAvisoAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<NotificacaoEmailResponse>(() => http.PostAsync($"api/notificacoes/ordens-servico/{id}/enviar", null, ct), ct);
    public Task<ResultadoServico<NotificacaoEmailResponse>> TentarNovamenteAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<NotificacaoEmailResponse>(() => http.PostAsync($"api/notificacoes/ordens-servico/{id}/tentar-novamente", null, ct), ct);
    public Task<ResultadoServico<NotificacaoEmailResponse>> ReenviarAsync(Guid id,
        ReenviarAvisoVeiculoProntoRequest request, CancellationToken ct = default) =>
        EnviarAsync<NotificacaoEmailResponse>(() => http.PostAsJsonAsync($"api/notificacoes/ordens-servico/{id}/reenviar", request, ct), ct);

    private async Task<ResultadoServico<T>> ObterAsync<T>(string url, CancellationToken ct)
    { try { return await ConverterAsync<T>(await http.GetAsync(url, ct), ct); } catch (HttpRequestException) { return ResultadoServico<T>.Falha("Não foi possível acessar a API."); } }
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
