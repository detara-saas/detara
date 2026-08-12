using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Components.Authorization;

namespace Detara.Web.Seguranca;

public sealed class JwtAuthenticationStateProvider(TokenStorage tokenStorage)
    : AuthenticationStateProvider
{
    private static readonly AuthenticationState Anonimo =
        new(new ClaimsPrincipal(new ClaimsIdentity()));

    public override async Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        var token = await tokenStorage.ObterAsync();
        if (string.IsNullOrWhiteSpace(token))
        {
            return Anonimo;
        }

        try
        {
            var claims = LerClaims(token);
            var expiracao = claims.FirstOrDefault(x => x.Type == "exp")?.Value;
            if (long.TryParse(expiracao, out var segundos) &&
                DateTimeOffset.FromUnixTimeSeconds(segundos) <= DateTimeOffset.UtcNow)
            {
                await tokenStorage.RemoverAsync();
                return Anonimo;
            }

            return new AuthenticationState(
                new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt", "name", "role")));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            await tokenStorage.RemoverAsync();
            return Anonimo;
        }
    }

    public void NotificarLogin() => NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
    public void NotificarLogout() => NotifyAuthenticationStateChanged(Task.FromResult(Anonimo));

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
