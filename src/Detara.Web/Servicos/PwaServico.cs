using Microsoft.JSInterop;

namespace Detara.Web.Servicos;

public sealed class PwaServico(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private DotNetObjectReference<PwaServico>? _referenciaJs;
    private bool _inicializado;

    public bool NavegadorOnline { get; private set; } = true;
    public bool ServidorDisponivel { get; private set; } = true;
    public bool PodeInstalar { get; private set; }
    public bool Instalada { get; private set; }
    public bool AtualizacaoDisponivel { get; private set; }
    public bool SemConexao => !NavegadorOnline || !ServidorDisponivel;
    public event Action? Alterado;

    public async Task InicializarAsync()
    {
        if (_inicializado)
        {
            return;
        }

        _inicializado = true;
        _referenciaJs = DotNetObjectReference.Create(this);
        await jsRuntime.InvokeVoidAsync("detaraPwa.inicializar", _referenciaJs);
    }

    public async Task<bool> InstalarAsync()
    {
        if (!PodeInstalar || Instalada)
        {
            return false;
        }

        return await jsRuntime.InvokeAsync<bool>("detaraPwa.instalar");
    }

    public async Task<bool> AtualizarAsync()
    {
        if (!AtualizacaoDisponivel)
        {
            return false;
        }

        return await jsRuntime.InvokeAsync<bool>("detaraPwa.atualizar");
    }

    public void RegistrarFalhaApi()
    {
        if (!ServidorDisponivel)
        {
            return;
        }

        ServidorDisponivel = false;
        Alterado?.Invoke();
    }

    public void RegistrarRespostaApi()
    {
        if (ServidorDisponivel)
        {
            return;
        }

        ServidorDisponivel = true;
        Alterado?.Invoke();
    }

    [JSInvokable("AtualizarEstadoPwa")]
    public Task AtualizarEstadoPwaAsync(
        bool navegadorOnline,
        bool podeInstalar,
        bool instalada,
        bool atualizacaoDisponivel)
    {
        NavegadorOnline = navegadorOnline;
        PodeInstalar = podeInstalar && !instalada;
        Instalada = instalada;
        AtualizacaoDisponivel = atualizacaoDisponivel;
        Alterado?.Invoke();
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (_inicializado)
        {
            try
            {
                await jsRuntime.InvokeVoidAsync("detaraPwa.destruir");
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _referenciaJs?.Dispose();
    }
}
