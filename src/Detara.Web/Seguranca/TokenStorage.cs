using Microsoft.JSInterop;

namespace Detara.Web.Seguranca;

public sealed class TokenStorage(IJSRuntime jsRuntime)
{
    private const string Chave = "detara.token";

    public ValueTask<string?> ObterAsync() =>
        jsRuntime.InvokeAsync<string?>("localStorage.getItem", Chave);

    public ValueTask SalvarAsync(string token) =>
        jsRuntime.InvokeVoidAsync("localStorage.setItem", Chave, token);

    public ValueTask RemoverAsync() =>
        jsRuntime.InvokeVoidAsync("localStorage.removeItem", Chave);
}
