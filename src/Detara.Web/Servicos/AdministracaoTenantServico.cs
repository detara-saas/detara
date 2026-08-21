using System.Net.Http.Json;
using System.Text.Json;
using Detara.Contracts.AdministracaoTenant;
using Detara.Contracts.Comum;

namespace Detara.Web.Servicos;

public sealed class AdministracaoTenantServico(HttpClient http)
{
    public event Action? ContextoAlterado;
    public string EmpresaNomeAtual { get; private set; } = "Empresa";

    public Task<ResultadoServico<EmpresaTenantResponse>> ObterEmpresaAsync(CancellationToken ct = default) =>
        EnviarAsync<EmpresaTenantResponse>(() => http.GetAsync("api/empresa", ct), ct);

    public async Task<ResultadoServico<EmpresaTenantResponse>> AtualizarEmpresaAsync(
        AtualizarEmpresaTenantRequest request, CancellationToken ct = default)
    {
        var resultado = await EnviarAsync<EmpresaTenantResponse>(
            () => http.PutAsJsonAsync("api/empresa", request, ct), ct);
        if (resultado is { Sucesso: true, Resultado: not null })
        {
            DefinirEmpresa(resultado.Resultado.NomeFantasia);
        }
        return resultado;
    }

    public Task<ResultadoServico<PaginaResponse<UsuarioTenantListaResponse>>> ListarUsuariosAsync(
        int pagina, string? pesquisa, string? status, CancellationToken ct = default)
    {
        var rota = $"api/usuarios?pagina={pagina}&tamanhoPagina=25";
        if (!string.IsNullOrWhiteSpace(pesquisa)) rota += $"&pesquisa={Uri.EscapeDataString(pesquisa.Trim())}";
        if (!string.IsNullOrWhiteSpace(status)) rota += $"&status={Uri.EscapeDataString(status)}";
        return EnviarAsync<PaginaResponse<UsuarioTenantListaResponse>>(() => http.GetAsync(rota, ct), ct);
    }

    public Task<ResultadoServico<UsuarioTenantDetalheResponse>> ObterUsuarioAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<UsuarioTenantDetalheResponse>(() => http.GetAsync($"api/usuarios/{id}", ct), ct);

    public Task<ResultadoServico<UsuarioTenantDetalheResponse>> ConvidarUsuarioAsync(
        ConvidarUsuarioTenantRequest request, CancellationToken ct = default) =>
        EnviarAsync<UsuarioTenantDetalheResponse>(() => http.PostAsJsonAsync("api/usuarios", request, ct), ct);

    public Task<ResultadoServico<UsuarioTenantDetalheResponse>> AlterarPerfilUsuarioAsync(
        Guid id, AlterarPerfilUsuarioTenantRequest request, CancellationToken ct = default) =>
        EnviarAsync<UsuarioTenantDetalheResponse>(() => http.PutAsJsonAsync($"api/usuarios/{id}/perfil", request, ct), ct);

    public Task<ResultadoServico<UsuarioTenantDetalheResponse>> AlterarStatusUsuarioAsync(
        Guid id, bool ativar, long versao, CancellationToken ct = default) =>
        EnviarAsync<UsuarioTenantDetalheResponse>(() => http.PostAsJsonAsync(
            $"api/usuarios/{id}/{(ativar ? "reativar" : "inativar")}",
            new AlterarStatusUsuarioTenantRequest(versao), ct), ct);

    public Task<ResultadoServico<UsuarioTenantDetalheResponse>> ReenviarConviteAsync(
        Guid id, CancellationToken ct = default) =>
        EnviarAsync<UsuarioTenantDetalheResponse>(() => http.PostAsync($"api/usuarios/{id}/convite/reenviar", null, ct), ct);

    public Task<ResultadoServico<IReadOnlyCollection<PerfilTenantResumoResponse>>> ListarPerfisAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyCollection<PerfilTenantResumoResponse>>(() => http.GetAsync("api/perfis", ct), ct);

    public Task<ResultadoServico<PerfilTenantDetalheResponse>> ObterPerfilAsync(Guid id, CancellationToken ct = default) =>
        EnviarAsync<PerfilTenantDetalheResponse>(() => http.GetAsync($"api/perfis/{id}", ct), ct);

    public Task<ResultadoServico<IReadOnlyCollection<PermissaoTenantResponse>>> ListarPermissoesAsync(CancellationToken ct = default) =>
        EnviarAsync<IReadOnlyCollection<PermissaoTenantResponse>>(() => http.GetAsync("api/perfis/permissoes", ct), ct);

    public Task<ResultadoServico<PerfilTenantDetalheResponse>> CriarPerfilAsync(
        SalvarPerfilTenantRequest request, CancellationToken ct = default) =>
        EnviarAsync<PerfilTenantDetalheResponse>(() => http.PostAsJsonAsync("api/perfis", request, ct), ct);

    public Task<ResultadoServico<PerfilTenantDetalheResponse>> AtualizarPerfilAsync(
        Guid id, SalvarPerfilTenantRequest request, CancellationToken ct = default) =>
        EnviarAsync<PerfilTenantDetalheResponse>(() => http.PutAsJsonAsync($"api/perfis/{id}", request, ct), ct);

    public Task<ResultadoServico<PerfilTenantDetalheResponse>> AlterarStatusPerfilAsync(
        Guid id, bool ativar, long versao, CancellationToken ct = default) =>
        EnviarAsync<PerfilTenantDetalheResponse>(() => http.PostAsJsonAsync(
            $"api/perfis/{id}/{(ativar ? "reativar" : "inativar")}",
            new AlterarStatusPerfilTenantRequest(versao), ct), ct);

    public async Task<ResultadoServico<MinhaContaResponse>> ObterMinhaContaAsync(CancellationToken ct = default)
    {
        var resultado = await EnviarAsync<MinhaContaResponse>(() => http.GetAsync("api/minha-conta", ct), ct);
        if (resultado is { Sucesso: true, Resultado: not null })
        {
            DefinirEmpresa(resultado.Resultado.EmpresaNome);
        }
        return resultado;
    }

    public Task<ResultadoServico<MinhaContaResponse>> AtualizarNomeAsync(
        AtualizarNomeMinhaContaRequest request, CancellationToken ct = default) =>
        EnviarAsync<MinhaContaResponse>(() => http.PutAsJsonAsync("api/minha-conta/nome", request, ct), ct);

    public Task<ResultadoServico<object>> AtualizarEmailAsync(
        AtualizarEmailMinhaContaRequest request, CancellationToken ct = default) =>
        EnviarAsync<object>(() => http.PutAsJsonAsync("api/minha-conta/email", request, ct), ct);

    public Task<ResultadoServico<object>> AlterarSenhaAsync(
        AlterarSenhaMinhaContaRequest request, CancellationToken ct = default) =>
        EnviarAsync<object>(() => http.PutAsJsonAsync("api/minha-conta/senha", request, ct), ct);

    private static async Task<ResultadoServico<T>> EnviarAsync<T>(
        Func<Task<HttpResponseMessage>> enviar,
        CancellationToken ct)
    {
        try
        {
            using var response = await enviar();
            if (response.Content.Headers.ContentLength == 0)
            {
                return ResultadoServico<T>.Falha("Não foi possível concluir a operação.");
            }

            var resposta = await response.Content.ReadFromJsonAsync<RespostaApi<T>>(ct);
            return response.IsSuccessStatusCode && resposta is { Sucesso: true, Resultado: not null }
                ? ResultadoServico<T>.Ok(resposta.Resultado, resposta.Info)
                : ResultadoServico<T>.Falha(resposta?.Info ?? "Não foi possível concluir a operação.");
        }
        catch (HttpRequestException)
        {
            return ResultadoServico<T>.Falha("A API não está disponível no momento.");
        }
        catch (TaskCanceledException) when (!ct.IsCancellationRequested)
        {
            return ResultadoServico<T>.Falha("A API não respondeu dentro do tempo esperado.");
        }
        catch (JsonException)
        {
            return ResultadoServico<T>.Falha("A API retornou uma resposta inválida.");
        }
    }

    private void DefinirEmpresa(string nome)
    {
        if (string.Equals(EmpresaNomeAtual, nome, StringComparison.Ordinal)) return;
        EmpresaNomeAtual = nome;
        ContextoAlterado?.Invoke();
    }
}
