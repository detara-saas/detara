using System.Net.Http.Json;
using Detara.Contracts.Autenticacao;
using Detara.Contracts.Comum;
using Detara.Web.Seguranca;

namespace Detara.Web.Servicos;

public sealed class AutenticacaoServico(
    HttpClient httpClient,
    TokenStorage tokenStorage,
    JwtAuthenticationStateProvider authenticationStateProvider,
    PreferenciasInterfaceServico preferencias)
{
    private SelecaoEmpresaPendente? _selecaoPendente;

    public SelecaoEmpresaPendente? SelecaoPendente =>
        _selecaoPendente is { ExpiraEmUtc: var expiracao } && expiracao > DateTime.UtcNow
            ? _selecaoPendente
            : null;

    public async Task<ResultadoLoginWeb> EntrarAsync(
        LoginRequest request,
        string destino,
        CancellationToken cancellationToken = default)
    {
        _selecaoPendente = null;
        var response = await httpClient.PostAsJsonAsync("api/autenticacao/login", request, cancellationToken);
        var resposta = await response.Content.ReadFromJsonAsync<RespostaApi<LoginResponse>>(cancellationToken);

        if (!response.IsSuccessStatusCode || resposta is not { Sucesso: true, Resultado: not null })
        {
            return new ResultadoLoginWeb(
                false,
                false,
                resposta?.Info ?? "Não foi possível entrar. Tente novamente.");
        }

        switch (resposta.Resultado)
        {
            case LoginAutenticadoResponse sessao:
                await ConcluirLoginAsync(sessao, cancellationToken);
                return new ResultadoLoginWeb(true, false, resposta.Info);
            case SelecaoEmpresaNecessariaResponse selecao:
                _selecaoPendente = new SelecaoEmpresaPendente(
                    selecao.Challenge,
                    selecao.ExpiraEmUtc,
                    selecao.Empresas,
                    destino);
                return new ResultadoLoginWeb(true, true, resposta.Info);
            default:
                return new ResultadoLoginWeb(false, false, "Resposta de autenticação inválida.");
        }
    }

    public async Task<(bool Sucesso, string Mensagem)> SelecionarEmpresaAsync(
        Guid empresaId,
        CancellationToken cancellationToken = default)
    {
        var pendente = SelecaoPendente;
        if (pendente is null)
        {
            _selecaoPendente = null;
            return (false, "A seleção expirou. Entre novamente.");
        }

        var request = new SelecionarEmpresaRequest(pendente.Challenge, empresaId);
        var response = await httpClient.PostAsJsonAsync(
            "api/autenticacao/selecionar-empresa",
            request,
            cancellationToken);
        var resposta = await response.Content
            .ReadFromJsonAsync<RespostaApi<LoginAutenticadoResponse>>(cancellationToken);

        if (!response.IsSuccessStatusCode || resposta is not { Sucesso: true, Resultado: not null })
        {
            if (resposta?.Erro?.Codigo == "selecao_empresa_invalida")
            {
                _selecaoPendente = null;
            }

            return (false, resposta?.Info ?? "Não foi possível selecionar a empresa.");
        }

        await ConcluirLoginAsync(resposta.Resultado, cancellationToken);
        return (true, resposta.Info);
    }

    public void CancelarSelecaoEmpresa() => _selecaoPendente = null;

    private async Task ConcluirLoginAsync(
        LoginAutenticadoResponse sessao,
        CancellationToken cancellationToken)
    {
        _selecaoPendente = null;
        await tokenStorage.SalvarAsync(sessao.Token);
        authenticationStateProvider.NotificarLogin();
        await preferencias.SincronizarAsync(cancellationToken);
    }

    public async Task SairAsync()
    {
        _selecaoPendente = null;
        await tokenStorage.RemoverAsync();
        authenticationStateProvider.NotificarLogout();
    }
}

public sealed record ResultadoLoginWeb(bool Sucesso, bool RequerSelecaoEmpresa, string Mensagem);

public sealed record SelecaoEmpresaPendente(
    string Challenge,
    DateTime ExpiraEmUtc,
    IReadOnlyCollection<EmpresaSelecaoResponse> Empresas,
    string Destino);
