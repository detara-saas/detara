using Microsoft.JSInterop;

namespace Detara.Web.Servicos;

public enum EstadoConectividade
{
    Online,
    Offline,
    Reconectando,
    Restabelecida,
    Falha
}

public sealed class PwaServico(IJSRuntime jsRuntime) : IAsyncDisposable
{
    private DotNetObjectReference<PwaServico>? _referenciaJs;
    private CancellationTokenSource? _ocultarRestabelecida;
    private bool _inicializado;

    public bool NavegadorOnline { get; private set; } = true;
    public bool ServidorDisponivel { get; private set; } = true;
    public bool PodeInstalar { get; private set; }
    public bool Instalada { get; private set; }
    public bool AtualizacaoDisponivel { get; private set; }
    public EstadoConectividade Conectividade { get; private set; } = EstadoConectividade.Online;
    public bool SemConexao => Conectividade is EstadoConectividade.Offline or EstadoConectividade.Reconectando or EstadoConectividade.Falha;
    public bool ExibirConectividade => Conectividade != EstadoConectividade.Online;
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
        ServidorDisponivel = false;
        DefinirConectividade(NavegadorOnline ? EstadoConectividade.Falha : EstadoConectividade.Offline);
    }

    public void RegistrarRespostaApi()
    {
        var deveConfirmarRestabelecimento = !ServidorDisponivel ||
            Conectividade is EstadoConectividade.Offline or EstadoConectividade.Reconectando or EstadoConectividade.Falha;
        ServidorDisponivel = true;
        if (!NavegadorOnline)
        {
            DefinirConectividade(EstadoConectividade.Offline);
            return;
        }

        if (deveConfirmarRestabelecimento)
        {
            DefinirConectividade(EstadoConectividade.Restabelecida);
            AgendarOcultacaoRestabelecida();
        }
    }

    public void IniciarReconexao()
    {
        DefinirConectividade(NavegadorOnline
            ? EstadoConectividade.Reconectando
            : EstadoConectividade.Offline);
    }

    [JSInvokable("AtualizarEstadoPwa")]
    public Task AtualizarEstadoPwaAsync(
        bool navegadorOnline,
        bool podeInstalar,
        bool instalada,
        bool atualizacaoDisponivel)
    {
        var navegadorEstavaOffline = !NavegadorOnline;
        NavegadorOnline = navegadorOnline;
        PodeInstalar = podeInstalar && !instalada;
        Instalada = instalada;
        AtualizacaoDisponivel = atualizacaoDisponivel;
        if (!navegadorOnline)
        {
            DefinirConectividade(EstadoConectividade.Offline, notificarSeIgual: true);
        }
        else if (navegadorEstavaOffline)
        {
            ServidorDisponivel = false;
            DefinirConectividade(EstadoConectividade.Reconectando, notificarSeIgual: true);
        }
        else
        {
            Alterado?.Invoke();
        }
        return Task.CompletedTask;
    }

    private void DefinirConectividade(EstadoConectividade estado, bool notificarSeIgual = false)
    {
        if (Conectividade == estado && !notificarSeIgual)
        {
            return;
        }

        if (estado != EstadoConectividade.Restabelecida)
        {
            CancelarOcultacaoRestabelecida();
        }

        Conectividade = estado;
        Alterado?.Invoke();
    }

    private void AgendarOcultacaoRestabelecida()
    {
        CancelarOcultacaoRestabelecida();
        _ocultarRestabelecida = new CancellationTokenSource();
        _ = OcultarRestabelecidaAsync(_ocultarRestabelecida.Token);
    }

    private async Task OcultarRestabelecidaAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(4), cancellationToken);
            if (Conectividade == EstadoConectividade.Restabelecida)
            {
                Conectividade = EstadoConectividade.Online;
                Alterado?.Invoke();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void CancelarOcultacaoRestabelecida()
    {
        _ocultarRestabelecida?.Cancel();
        _ocultarRestabelecida?.Dispose();
        _ocultarRestabelecida = null;
    }

    public async ValueTask DisposeAsync()
    {
        CancelarOcultacaoRestabelecida();
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
