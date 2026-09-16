using System.Net.Http.Json;
using Detara.Contracts.Assinaturas;
using Detara.Contracts.Comum;

namespace Detara.Web.Servicos;

public sealed class AssinaturasServico(HttpClient http)
{
    public Task<ResultadoServico<AssinaturaEmpresaResponse>> ObterAsync(CancellationToken ct = default) =>
        EnviarAsync(() => http.GetAsync("api/assinatura", ct), ct);

    public Task<ResultadoServico<AssinaturaEmpresaResponse>> AceitarAsync(CancellationToken ct = default) =>
        EnviarAsync(() => http.PostAsJsonAsync("api/assinatura/termo/aceite", new AceitarTermoAssinaturaRequest(true), ct), ct);

    public Task<(byte[]? Conteudo, string Nome, string Mensagem)> ObterTermoAsync(bool previa, CancellationToken ct = default) =>
        BaixarAsync(previa ? "api/assinatura/termo/previa" : "api/assinatura/termo/aceito", ct);

    private async Task<(byte[]? Conteudo, string Nome, string Mensagem)> BaixarAsync(string rota, CancellationToken ct)
    {
        try
        {
            using var response = await http.GetAsync(rota, ct);
            if (!response.IsSuccessStatusCode) return (null, string.Empty, "Não foi possível obter o termo.");
            var nome = response.Content.Headers.ContentDisposition?.FileNameStar?.Trim('"') ?? "termo-adesao-detara.pdf";
            return (await response.Content.ReadAsByteArrayAsync(ct), nome, string.Empty);
        }
        catch (HttpRequestException) { return (null, string.Empty, "Não foi possível acessar a API."); }
    }

    private static async Task<ResultadoServico<AssinaturaEmpresaResponse>> EnviarAsync(
        Func<Task<HttpResponseMessage>> enviar, CancellationToken ct)
    {
        try
        {
            using var response = await enviar();
            var envelope = await response.Content.ReadFromJsonAsync<RespostaApi<AssinaturaEmpresaResponse>>(ct);
            return response.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
                ? ResultadoServico<AssinaturaEmpresaResponse>.Ok(envelope.Resultado, envelope.Info)
                : ResultadoServico<AssinaturaEmpresaResponse>.Falha(envelope?.Info ?? "Não foi possível concluir a operação.");
        }
        catch (HttpRequestException) { return ResultadoServico<AssinaturaEmpresaResponse>.Falha("Não foi possível acessar a API."); }
    }
}
