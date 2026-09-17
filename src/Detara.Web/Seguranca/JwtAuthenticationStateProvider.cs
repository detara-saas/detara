using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Detara.Web.Seguranca;

public sealed class JwtAuthenticationStateProvider(
    TokenStorage tokenStorage,
    SessaoRefreshServico refreshServico)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonimo =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    private readonly object _sincronizacao = new();
    private Task<AuthenticationState>? _inicializacao;

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        lock (_sincronizacao)
        {
            return _inicializacao ??= InicializarAsync();
        }
    }

    private async Task<AuthenticationState> InicializarAsync()
    {
        await Task.Yield();
        var token = await tokenStorage.ObterAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            var estado = await CriarEstadoDoTokenAsync(token, removerInvalido: false);
            if (estado.User.Identity?.IsAuthenticated == true)
            {
                return estado;
            }
        }

        var refresh = await refreshServico.RenovarTenantAsync();
        if (refresh == ResultadoRefresh.Sucesso)
        {
            return await CriarEstadoDoTokenAsync(await tokenStorage.ObterAsync(), removerInvalido: true);
        }

        if (refresh == ResultadoRefresh.Invalido)
        {
            await tokenStorage.RemoverAsync();
        }
        else
        {
            lock (_sincronizacao) _inicializacao = null;
        }

        return Anonimo;
    }

    public void NotificarLogin()
    {
        var estado = CriarEstadoAtualAsync();
        lock (_sincronizacao) _inicializacao = estado;
        NotifyAuthenticationStateChanged(estado);
    }

    private async Task<AuthenticationState> CriarEstadoAtualAsync() =>
        await CriarEstadoDoTokenAsync(await tokenStorage.ObterAsync(), removerInvalido: true);

    public void NotificarLogout()
    {
        var estado = Task.FromResult(Anonimo);
        lock (_sincronizacao) _inicializacao = estado;
        NotifyAuthenticationStateChanged(estado);
    }

    private async Task<AuthenticationState> CriarEstadoDoTokenAsync(
        string? token,
        bool removerInvalido)
    {
        if (string.IsNullOrWhiteSpace(token)) return Anonimo;
        try
        {
            var claims = LerClaims(token);
            var expiracao = claims.FirstOrDefault(x => x.Type == "exp")?.Value;
            if (!long.TryParse(expiracao, out var segundos) ||
                DateTimeOffset.FromUnixTimeSeconds(segundos) <= DateTimeOffset.UtcNow)
            {
                if (removerInvalido) await tokenStorage.RemoverAsync();
                return Anonimo;
            }

            return new AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", "name", "role")));
        }
        catch (Exception exception) when (
            exception is FormatException or JsonException or ArgumentOutOfRangeException)
        {
            if (removerInvalido) await tokenStorage.RemoverAsync();
            return Anonimo;
        }
    }

    private static IReadOnlyCollection<Claim> LerClaims(string token)
    {
        var partes = token.Split('.');
        if (partes.Length != 3)
        {
            throw new FormatException("Token JWT inválido.");
        }

        var payload = partes[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + ((4 - payload.Length % 4) % 4), '=');
        var json = Convert.FromBase64String(payload);
        var valores = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
            ?? throw new JsonException("Payload JWT inválido.");
        var claims = new List<Claim>();

        foreach (var (tipo, valor) in valores)
        {
            if (valor.ValueKind == JsonValueKind.Array)
            {
                claims.AddRange(valor.EnumerateArray().Select(item => new Claim(tipo, item.ToString())));
            }
            else
            {
                claims.Add(new Claim(tipo, valor.ToString()));
            }
        }

        return claims;
    }
}
