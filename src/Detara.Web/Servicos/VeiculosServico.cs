using System.Net.Http.Json;
using Detara.Contracts.Clientes;
using Detara.Contracts.Comum;
using Detara.Contracts.Veiculos;

namespace Detara.Web.Servicos;

public sealed class VeiculosServico(HttpClient httpClient)
{
    public async Task<ResultadoServico<PaginaResponse<VeiculoListaResponse>>> ListarAsync(
        int pagina,
        int tamanhoPagina,
        string? pesquisa,
        bool? ehAtivo,
        CancellationToken cancellationToken = default)
    {
        var parametros = new List<string>
        {
            $"pagina={pagina}",
            $"tamanhoPagina={tamanhoPagina}"
        };
        Adicionar(parametros, "pesquisa", pesquisa);
        Adicionar(parametros, "ehAtivo", ehAtivo?.ToString().ToLowerInvariant());
        return await ObterAsync<PaginaResponse<VeiculoListaResponse>>(
            $"api/veiculos?{string.Join('&', parametros)}",
            cancellationToken);
    }

    public Task<ResultadoServico<VeiculoDetalheResponse>> ObterAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        ObterAsync<VeiculoDetalheResponse>($"api/veiculos/{id}", cancellationToken);

    public Task<ResultadoServico<VeiculoDetalheResponse>> CriarAsync(
        SalvarVeiculoRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<VeiculoDetalheResponse>(
            () => httpClient.PostAsJsonAsync("api/veiculos", request, cancellationToken),
            cancellationToken);

    public Task<ResultadoServico<VeiculoDetalheResponse>> AtualizarAsync(
        Guid id,
        SalvarVeiculoRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<VeiculoDetalheResponse>(
            () => httpClient.PutAsJsonAsync($"api/veiculos/{id}", request, cancellationToken),
            cancellationToken);

    public async Task<ResultadoServico<bool>> AlterarStatusAsync(
        Guid id,
        bool ehAtivo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PatchAsJsonAsync(
                $"api/veiculos/{id}/status",
                new AlterarStatusRequest(ehAtivo),
                cancellationToken);
            return response.IsSuccessStatusCode
                ? ResultadoServico<bool>.Ok(true, ehAtivo ? "Veículo ativado." : "Veículo inativado.")
                : ResultadoServico<bool>.Falha(await LerMensagemAsync(response, cancellationToken));
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<bool>.Falha("Não foi possível acessar a API.");
        }
    }

    private async Task<ResultadoServico<T>> ObterAsync<T>(string endereco, CancellationToken cancellationToken)
    {
        try
        {
            return await ConverterAsync<T>(await httpClient.GetAsync(endereco, cancellationToken), cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<T>.Falha("Não foi possível acessar a API.");
        }
    }

    private async Task<ResultadoServico<T>> EnviarAsync<T>(
        Func<Task<HttpResponseMessage>> enviar,
        CancellationToken cancellationToken)
    {
        try
        {
            return await ConverterAsync<T>(await enviar(), cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<T>.Falha("Não foi possível acessar a API.");
        }
    }

    private static async Task<ResultadoServico<T>> ConverterAsync<T>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var envelope = await response.Content.ReadFromJsonAsync<RespostaApi<T>>(cancellationToken);
        return response.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
            ? ResultadoServico<T>.Ok(envelope.Resultado, envelope.Info)
            : ResultadoServico<T>.Falha(envelope?.Info ?? "Não foi possível concluir a operação.");
    }

    private static async Task<string> LerMensagemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var envelope = await response.Content.ReadFromJsonAsync<RespostaApi<object>>(cancellationToken);
        return envelope?.Info ?? "Não foi possível concluir a operação.";
    }

    private static void Adicionar(ICollection<string> parametros, string nome, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            parametros.Add($"{nome}={Uri.EscapeDataString(valor)}");
        }
    }
}
