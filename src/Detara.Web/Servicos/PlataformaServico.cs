using System.Net.Http.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Plataforma;
using Detara.Web.Seguranca;

namespace Detara.Web.Servicos;

public sealed class PlataformaServico(HttpClientPlataforma cliente, PlatformTokenStorage storage)
{
    private const int TamanhoPaginaPadrao = 25;
    private readonly HttpClient _http = cliente.Valor;

    public async Task<ResultadoServico<DesafioMfaPlataformaResponse>> EntrarAsync(
        LoginPlataformaRequest request,
        CancellationToken cancellationToken = default)
    {
        var resultado = await EnviarAsync<DesafioMfaPlataformaResponse>(
            () => _http.PostAsJsonAsync("api/plataforma/autenticacao/login", request, cancellationToken),
            cancellationToken);
        if (resultado is { Sucesso: true, Resultado: not null })
        {
            await storage.SalvarDesafioAsync(resultado.Resultado.Desafio);
        }

        return resultado;
    }

    public async Task<ResultadoServico<ConfiguracaoMfaPlataformaResponse>> ObterConfiguracaoMfaAsync(
        CancellationToken cancellationToken = default)
    {
        var desafio = await storage.ObterDesafioAsync();
        return string.IsNullOrWhiteSpace(desafio)
            ? ResultadoServico<ConfiguracaoMfaPlataformaResponse>.Falha("O desafio expirou. Entre novamente.")
            : await EnviarAsync<ConfiguracaoMfaPlataformaResponse>(
                () => _http.PostAsJsonAsync(
                    "api/plataforma/autenticacao/mfa/configuracao",
                    new DesafioMfaRequest(desafio),
                    cancellationToken),
                cancellationToken);
    }

    public async Task<ResultadoServico<SessaoPlataformaResponse>> ConcluirMfaAsync(
        string codigo,
        bool ativacao,
        CancellationToken cancellationToken = default)
    {
        var desafio = await storage.ObterDesafioAsync();
        if (string.IsNullOrWhiteSpace(desafio))
        {
            return ResultadoServico<SessaoPlataformaResponse>.Falha("O desafio expirou. Entre novamente.");
        }

        var rota = ativacao ? "mfa/ativar" : "mfa/verificar";
        var resultado = await EnviarAsync<SessaoPlataformaResponse>(
            () => _http.PostAsJsonAsync(
                $"api/plataforma/autenticacao/{rota}",
                new VerificarMfaPlataformaRequest(desafio, codigo),
                cancellationToken),
            cancellationToken);
        if (resultado is { Sucesso: true, Resultado: not null })
        {
            await storage.SalvarTokenAsync(resultado.Resultado.Token);
            await storage.RemoverDesafioAsync();
        }

        return resultado;
    }

    public ValueTask<string?> ObterTokenAsync() => storage.ObterTokenAsync();

    public async Task SairAsync()
    {
        await storage.RemoverTokenAsync();
        await storage.RemoverDesafioAsync();
    }

    public Task<ResultadoServico<DashboardPlataformaResponse>> ObterDashboardAsync(
        CancellationToken cancellationToken = default) =>
        ObterAsync<DashboardPlataformaResponse>("api/plataforma/dashboard", cancellationToken);

    public Task<ResultadoServico<PaginaResponse<EmpresaPlataformaResumoResponse>>> ListarEmpresasAsync(
        int pagina,
        string? pesquisa,
        bool? ativa,
        CancellationToken cancellationToken = default)
    {
        var query = $"?pagina={pagina}&tamanhoPagina={TamanhoPaginaPadrao}";
        if (!string.IsNullOrWhiteSpace(pesquisa))
        {
            query += $"&pesquisa={Uri.EscapeDataString(pesquisa.Trim())}";
        }

        if (ativa is not null)
        {
            query += $"&ativa={ativa.Value.ToString().ToLowerInvariant()}";
        }

        return ObterAsync<PaginaResponse<EmpresaPlataformaResumoResponse>>(
            $"api/plataforma/empresas{query}",
            cancellationToken);
    }

    public Task<ResultadoServico<EmpresaPlataformaDetalheResponse>> ObterEmpresaAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        ObterAsync<EmpresaPlataformaDetalheResponse>($"api/plataforma/empresas/{id}", cancellationToken);

    public Task<ResultadoServico<EmpresaPlataformaDetalheResponse>> ProvisionarAsync(
        ProvisionarEmpresaRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<EmpresaPlataformaDetalheResponse>(
            () => _http.PostAsJsonAsync("api/plataforma/empresas", request, cancellationToken),
            cancellationToken);

    public Task<ResultadoServico<object>> AlterarStatusAsync(
        Guid id,
        bool suspender,
        string motivo,
        CancellationToken cancellationToken = default) =>
        EnviarSemConteudoAsync(
            () => _http.PostAsJsonAsync(
                $"api/plataforma/empresas/{id}/{(suspender ? "suspender" : "reativar")}",
                new AlterarStatusEmpresaPlataformaRequest(motivo),
                cancellationToken),
            cancellationToken);

    public Task<ResultadoServico<object>> ReenviarConviteAsync(
        Guid id,
        CancellationToken cancellationToken = default) =>
        EnviarSemConteudoAsync(
            () => _http.PostAsync(
                $"api/plataforma/empresas/{id}/convite/reenviar",
                null,
                cancellationToken),
            cancellationToken);

    public Task<ResultadoServico<PaginaResponse<AuditoriaPlataformaItemResponse>>> ListarAuditoriaAsync(
        int pagina,
        string? tipo,
        CancellationToken cancellationToken = default)
    {
        var query = $"?pagina={pagina}&tamanhoPagina={TamanhoPaginaPadrao}";
        if (!string.IsNullOrWhiteSpace(tipo))
        {
            query += $"&tipo={Uri.EscapeDataString(tipo.Trim())}";
        }

        return ObterAsync<PaginaResponse<AuditoriaPlataformaItemResponse>>(
            $"api/plataforma/auditoria{query}",
            cancellationToken);
    }

    public Task<ResultadoServico<CodigosRecuperacaoResponse>> RegenerarCodigosAsync(
        RegenerarCodigosRecuperacaoRequest request,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<CodigosRecuperacaoResponse>(
            () => _http.PostAsJsonAsync(
                "api/plataforma/autenticacao/recovery-codes/regenerar",
                request,
                cancellationToken),
            cancellationToken);

    public Task<ResultadoServico<ConviteAdministradorValidadoResponse>> ValidarConviteAsync(
        string token,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<ConviteAdministradorValidadoResponse>(
            () => _http.PostAsJsonAsync(
                "api/convites/administrador/validar",
                new ValidarConviteAdministradorRequest(token),
                cancellationToken),
            cancellationToken);

    public Task<ResultadoServico<object>> AceitarConviteAsync(
        string token,
        string senha,
        CancellationToken cancellationToken = default) =>
        EnviarAsync<object>(
            () => _http.PostAsJsonAsync(
                "api/convites/administrador/aceitar",
                new AceitarConviteAdministradorRequest(token, senha),
                cancellationToken),
            cancellationToken);

    private Task<ResultadoServico<T>> ObterAsync<T>(string rota, CancellationToken cancellationToken) =>
        EnviarAsync<T>(() => _http.GetAsync(rota, cancellationToken), cancellationToken);

    private static async Task<ResultadoServico<T>> EnviarAsync<T>(
        Func<Task<HttpResponseMessage>> enviar,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await enviar();
            var resposta = await response.Content.ReadFromJsonAsync<RespostaApi<T>>(cancellationToken);
            return response.IsSuccessStatusCode && resposta is { Sucesso: true, Resultado: not null }
                ? ResultadoServico<T>.Ok(resposta.Resultado, resposta.Info)
                : ResultadoServico<T>.Falha(resposta?.Info ?? "Não foi possível concluir a operação.");
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<T>.Falha("A API não está disponível no momento.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return ResultadoServico<T>.Falha("A API não respondeu dentro do tempo esperado.");
        }
    }

    private static async Task<ResultadoServico<object>> EnviarSemConteudoAsync(
        Func<Task<HttpResponseMessage>> enviar,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await enviar();
            return response.IsSuccessStatusCode
                ? ResultadoServico<object>.Ok(new object())
                : ResultadoServico<object>.Falha("Não foi possível concluir a operação.");
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<object>.Falha("A API não está disponível no momento.");
        }
    }
}

public sealed class HttpClientPlataforma(HttpClient valor)
{
    public HttpClient Valor { get; } = valor;
}
