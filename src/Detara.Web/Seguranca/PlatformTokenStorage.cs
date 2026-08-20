using Microsoft.JSInterop;

namespace Detara.Web.Seguranca;

public sealed class PlatformTokenStorage(IJSRuntime jsRuntime)
{
    private const string ChaveToken = "detara.platform.token";
    private const string ChaveDesafio = "detara.platform.mfa-challenge";

    public ValueTask<string?> ObterTokenAsync() =>
        jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", ChaveToken);

    public ValueTask SalvarTokenAsync(string token) =>
        jsRuntime.InvokeVoidAsync("sessionStorage.setItem", ChaveToken, token);

    public ValueTask RemoverTokenAsync() =>
        jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", ChaveToken);

    public ValueTask<string?> ObterDesafioAsync() =>
        jsRuntime.InvokeAsync<string?>("sessionStorage.getItem", ChaveDesafio);

    public ValueTask SalvarDesafioAsync(string desafio) =>
        jsRuntime.InvokeVoidAsync("sessionStorage.setItem", ChaveDesafio, desafio);

    public ValueTask RemoverDesafioAsync() =>
        jsRuntime.InvokeVoidAsync("sessionStorage.removeItem", ChaveDesafio);
}
