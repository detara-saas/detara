using System.Net.Http.Json;
using System.Text.Json;
using Detara.Contracts.Comum;
using Detara.Contracts.Preferencias;
using Microsoft.JSInterop;

namespace Detara.Web.Servicos;

public sealed class PreferenciasInterfaceServico(HttpClient httpClient, IJSRuntime jsRuntime)
    : IAsyncDisposable
{
    private const string ChaveCache = "detara.preferencias";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private bool _sistemaEscuro;
    private DotNetObjectReference<PreferenciasInterfaceServico>? _referenciaJs;

    public PreferenciasUsuarioResponse Atual { get; private set; } = Padrao();
    public bool EhEscuro => Atual.Tema == "Escuro" || Atual.Tema == "Sistema" && _sistemaEscuro;
    public event Action? Alterado;

    public async Task InicializarAsync()
    {
        _sistemaEscuro = await jsRuntime.InvokeAsync<bool>("detara.preferenciasSistemaEscuro");
        _referenciaJs = DotNetObjectReference.Create(this);
        await jsRuntime.InvokeVoidAsync("detara.observarTemaSistema", _referenciaJs);
        var json = await jsRuntime.InvokeAsync<string?>("localStorage.getItem", ChaveCache);
        if (!string.IsNullOrWhiteSpace(json))
        {
            try
            {
                Atual = JsonSerializer.Deserialize<PreferenciasUsuarioResponse>(json, JsonOptions) ?? Padrao();
            }
            catch (JsonException)
            {
                await jsRuntime.InvokeVoidAsync("localStorage.removeItem", ChaveCache);
            }
        }

        await AplicarTemaAsync();
    }

    public async Task SincronizarAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var resposta = await httpClient.GetFromJsonAsync<RespostaApi<PreferenciasUsuarioResponse>>(
                "api/preferencias/me",
                cancellationToken);
            if (resposta is { Sucesso: true, Resultado: not null })
            {
                Atual = resposta.Resultado;
                await SalvarCacheEAplicarAsync();
            }
        }
        catch (HttpRequestException)
        {
            // Cache local mantém a experiência responsiva quando a API está indisponível.
        }
    }

    public Task DefinirTemaAsync(string tema) => AtualizarAsync(Atual with { Tema = tema });
    public Task DefinirSidebarAsync(bool recolhida) =>
        AtualizarAsync(Atual with { SidebarRecolhida = recolhida });

    public async Task AtualizarAsync(
        PreferenciasUsuarioResponse preferencias,
        CancellationToken cancellationToken = default)
    {
        Atual = preferencias;
        await SalvarCacheEAplicarAsync();

        try
        {
            var request = new AtualizarPreferenciasUsuarioRequest(
                Atual.Tema,
                Atual.Idioma,
                Atual.SidebarRecolhida,
                Atual.PaginaInicial,
                Atual.Favoritos);
            await httpClient.PutAsJsonAsync("api/preferencias/me", request, cancellationToken);
        }
        catch (HttpRequestException)
        {
            // A preferência permanece no cache e será reconciliada no próximo login.
        }
    }

    private async Task SalvarCacheEAplicarAsync()
    {
        await jsRuntime.InvokeVoidAsync(
            "localStorage.setItem",
            ChaveCache,
            JsonSerializer.Serialize(Atual, JsonOptions));
        await AplicarTemaAsync();
        Alterado?.Invoke();
    }

    private ValueTask AplicarTemaAsync() =>
        jsRuntime.InvokeVoidAsync("detara.aplicarTema", EhEscuro);

    [JSInvokable("AtualizarTemaSistema")]
    public async Task AtualizarTemaSistemaAsync(bool escuro)
    {
        _sistemaEscuro = escuro;
        if (Atual.Tema == "Sistema")
        {
            await AplicarTemaAsync();
            Alterado?.Invoke();
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await jsRuntime.InvokeVoidAsync("detara.pararObservacaoTemaSistema");
        }
        catch (JSDisconnectedException)
        {
        }

        _referenciaJs?.Dispose();
    }

    private static PreferenciasUsuarioResponse Padrao() =>
        new("Sistema", "pt-BR", false, "dashboard", []);
}
