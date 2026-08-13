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
    public async Task<(bool Sucesso, string Mensagem)> EntrarAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var response = await httpClient.PostAsJsonAsync("api/autenticacao/login", request, cancellationToken);
        var resposta = await response.Content.ReadFromJsonAsync<RespostaApi<LoginResponse>>(cancellationToken);

        if (!response.IsSuccessStatusCode || resposta is not { Sucesso: true, Resultado: not null })
        {
            return (false, resposta?.Info ?? "Não foi possível entrar. Tente novamente.");
        }

        await tokenStorage.SalvarAsync(resposta.Resultado.Token);
        authenticationStateProvider.NotificarLogin();
        await preferencias.SincronizarAsync(cancellationToken);
        return (true, resposta.Info);
    }

    public async Task SairAsync()
    {
        await tokenStorage.RemoverAsync();
        authenticationStateProvider.NotificarLogout();
    }
}
