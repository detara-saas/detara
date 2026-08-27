using System.Net.Http.Json;
using Detara.Contracts.Clientes;
using Detara.Contracts.Comum;

namespace Detara.Web.Servicos;

public sealed class ClientesServico(HttpClient httpClient)
{
    public Task<ResultadoServico<PaginaResponse<ClienteListaResponse>>> ListarAsync(
        int pagina,
        int tamanhoPagina,
        string? pesquisa,
        bool? ehAtivo,
        string? tipoPessoa,
        CancellationToken cancellationToken = default)
    {
        var parametros = new List<string>
        {
            $"pagina={pagina}",
            $"tamanhoPagina={tamanhoPagina}"
        };
        Adicionar(parametros, "pesquisa", pesquisa);
        Adicionar(parametros, "ehAtivo", ehAtivo?.ToString().ToLowerInvariant());
        Adicionar(parametros, "tipoPessoa", tipoPessoa);
        return ObterAsync<PaginaResponse<ClienteListaResponse>>(
            $"api/clientes?{string.Join('&', parametros)}",
            cancellationToken);
    }

    public Task<ResultadoServico<ClienteDetalheResponse>> ObterAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        ObterAsync<ClienteDetalheResponse>($"api/clientes/{id}", cancellationToken);

    public Task<ResultadoServico<ClienteRelacionamentoResponse>> ObterRelacionamentoAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        ObterAsync<ClienteRelacionamentoResponse>(
            $"api/clientes/{id}/relacionamento",
            cancellationToken);

    public Task<ResultadoServico<IReadOnlyCollection<ClienteBuscaResponse>>> BuscarAsync(
        string pesquisa,
        CancellationToken cancellationToken = default) =>
        ObterAsync<IReadOnlyCollection<ClienteBuscaResponse>>(
            $"api/clientes/busca?pesquisa={Uri.EscapeDataString(pesquisa)}&limite=15",
            cancellationToken);

    public Task<ResultadoServico<ClienteDetalheResponse>> CriarAsync(
        SalvarClienteRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<ClienteDetalheResponse>(
            () => httpClient.PostAsJsonAsync("api/clientes", request, cancellationToken),
            cancellationToken);

    public Task<ResultadoServico<ClienteDetalheResponse>> AtualizarAsync(
        Guid id,
        SalvarClienteRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<ClienteDetalheResponse>(
            () => httpClient.PutAsJsonAsync($"api/clientes/{id}", request, cancellationToken),
            cancellationToken);

    public async Task<ResultadoServico<bool>> AlterarStatusAsync(
        Guid id,
        bool ehAtivo,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.PatchAsJsonAsync(
                $"api/clientes/{id}/status",
                new AlterarStatusRequest(ehAtivo),
                cancellationToken);
            return response.IsSuccessStatusCode
                ? ResultadoServico<bool>.Ok(true, ehAtivo ? "Cliente ativado." : "Cliente inativado.")
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
            var response = await httpClient.GetAsync(endereco, cancellationToken);
            return await ConverterAsync<T>(response, cancellationToken);
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
