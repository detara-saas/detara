using System.Net.Http.Json;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components.Forms;
using Detara.Contracts.Clientes;
using Detara.Contracts.Comum;
using Detara.Contracts.Veiculos;

namespace Detara.Web.Servicos;

public sealed class VeiculosServico(HttpClient httpClient)
{
    public const long TamanhoMaximoFotoBytes = 10L * 1024 * 1024;
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
            using var response = await httpClient.PatchAsJsonAsync(
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

    public Task<ResultadoServico<IReadOnlyCollection<VeiculoFotoResponse>>> ListarFotosAsync(
        Guid veiculoId,
        CancellationToken cancellationToken = default) =>
        ObterAsync<IReadOnlyCollection<VeiculoFotoResponse>>(
            $"api/veiculos/{veiculoId}/fotos",
            cancellationToken);

    public async Task<ResultadoServico<VeiculoFotoResponse>> EnviarFotoAsync(
        Guid veiculoId,
        IBrowserFile arquivo,
        CancellationToken cancellationToken = default)
    {
        if (arquivo.Size == 0)
        {
            return ResultadoServico<VeiculoFotoResponse>.Falha("O arquivo não pode estar vazio.");
        }

        if (arquivo.Size > TamanhoMaximoFotoBytes)
        {
            return ResultadoServico<VeiculoFotoResponse>.Falha("A foto deve possuir no máximo 10 MiB.");
        }

        try
        {
            await using var stream = arquivo.OpenReadStream(TamanhoMaximoFotoBytes, cancellationToken);
            using var conteudo = new StreamContent(stream);
            if (MediaTypeHeaderValue.TryParse(arquivo.ContentType, out var contentType))
            {
                conteudo.Headers.ContentType = contentType;
            }

            using var formulario = new MultipartFormDataContent();
            formulario.Add(conteudo, "arquivo", arquivo.Name);
            return await ConverterAsync<VeiculoFotoResponse>(
                await httpClient.PostAsync(
                    $"api/veiculos/{veiculoId}/fotos",
                    formulario,
                    cancellationToken),
                cancellationToken);
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<VeiculoFotoResponse>.Falha("Não foi possível acessar a API.");
        }
        catch (IOException exception)
        {
            return ResultadoServico<VeiculoFotoResponse>.Falha(exception.Message);
        }
    }

    public async Task<ResultadoServico<FotoConteudoDownload>> ObterConteudoFotoAsync(
        Guid veiculoId,
        Guid fotoId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await httpClient.GetAsync(
                $"api/veiculos/{veiculoId}/fotos/{fotoId}/conteudo",
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var mensagem = await LerMensagemAsync(response, cancellationToken);
                response.Dispose();
                return ResultadoServico<FotoConteudoDownload>.Falha(mensagem);
            }

            var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            return ResultadoServico<FotoConteudoDownload>.Ok(
                new FotoConteudoDownload(
                    response,
                    stream,
                    response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream"));
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<FotoConteudoDownload>.Falha("Não foi possível acessar a API.");
        }
    }

    public Task<ResultadoServico<bool>> DefinirFotoPrincipalAsync(
        Guid veiculoId,
        Guid fotoId,
        CancellationToken cancellationToken = default) =>
        ExecutarSemConteudoAsync(
            new HttpRequestMessage(
                HttpMethod.Patch,
                $"api/veiculos/{veiculoId}/fotos/{fotoId}/principal"),
            "Foto principal atualizada.",
            cancellationToken);

    public Task<ResultadoServico<bool>> ExcluirFotoAsync(
        Guid veiculoId,
        Guid fotoId,
        CancellationToken cancellationToken = default) =>
        ExecutarSemConteudoAsync(
            new HttpRequestMessage(
                HttpMethod.Delete,
                $"api/veiculos/{veiculoId}/fotos/{fotoId}"),
            "Foto excluída.",
            cancellationToken);

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
        using (response)
        {
            var envelope = await response.Content.ReadFromJsonAsync<RespostaApi<T>>(cancellationToken);
            return response.IsSuccessStatusCode && envelope is { Sucesso: true, Resultado: not null }
                ? ResultadoServico<T>.Ok(envelope.Resultado, envelope.Info)
                : ResultadoServico<T>.Falha(envelope?.Info ?? "Não foi possível concluir a operação.");
        }
    }

    private static async Task<string> LerMensagemAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var envelope = await response.Content.ReadFromJsonAsync<RespostaApi<object>>(cancellationToken);
        return envelope?.Info ?? "Não foi possível concluir a operação.";
    }

    private async Task<ResultadoServico<bool>> ExecutarSemConteudoAsync(
        HttpRequestMessage request,
        string mensagemSucesso,
        CancellationToken cancellationToken)
    {
        using (request)
        {
            try
            {
                using var response = await httpClient.SendAsync(request, cancellationToken);
                return response.IsSuccessStatusCode
                    ? ResultadoServico<bool>.Ok(true, mensagemSucesso)
                    : ResultadoServico<bool>.Falha(await LerMensagemAsync(response, cancellationToken));
            }
            catch (HttpRequestException)
            {
                return ResultadoServico<bool>.Falha("Não foi possível acessar a API.");
            }
        }
    }

    private static void Adicionar(ICollection<string> parametros, string nome, string? valor)
    {
        if (!string.IsNullOrWhiteSpace(valor))
        {
            parametros.Add($"{nome}={Uri.EscapeDataString(valor)}");
        }
    }
}

public sealed class FotoConteudoDownload(
    HttpResponseMessage response,
    Stream conteudo,
    string contentType) : IAsyncDisposable
{
    public Stream Conteudo { get; } = conteudo;
    public string ContentType { get; } = contentType;

    public async ValueTask DisposeAsync()
    {
        await Conteudo.DisposeAsync();
        response.Dispose();
    }
}
