using System.Net;
using System.Net.Http.Json;
using Detara.Contracts.Capacidades;
using Detara.Contracts.Comum;

namespace Detara.Web.Servicos;

public enum EstadoCarregamentoCapacidades
{
    NaoCarregado,
    Carregando,
    Carregado,
    FalhaRede,
    SessaoInvalida
}

public sealed class EmpresaCapacidadesState(HttpClient http)
{
    private Task? _carregamento;
    private SnapshotCapacidadesEmpresaResponse? _snapshot;

    public event Action? Alterado;
    public EstadoCarregamentoCapacidades Estado { get; private set; } = EstadoCarregamentoCapacidades.NaoCarregado;
    public string? Mensagem { get; private set; }
    public string? Segmento => _snapshot?.Segmento;

    public bool Possui(string codigo) =>
        Estado == EstadoCarregamentoCapacidades.Carregado &&
        _snapshot?.Capacidades.Any(item => item.Codigo == codigo && item.Habilitada) == true;

    public Task CarregarAsync(bool forcar = false)
    {
        if (forcar)
        {
            _carregamento = null;
            _snapshot = null;
            Estado = EstadoCarregamentoCapacidades.NaoCarregado;
        }

        if (Estado == EstadoCarregamentoCapacidades.Carregado) return Task.CompletedTask;
        return _carregamento ??= CarregarInternoAsync();
    }

    public async Task RecarregarAsync() => await CarregarAsync(true);

    public void Limpar()
    {
        _carregamento = null;
        _snapshot = null;
        Mensagem = null;
        Estado = EstadoCarregamentoCapacidades.NaoCarregado;
        Alterado?.Invoke();
    }

    private async Task CarregarInternoAsync()
    {
        Estado = EstadoCarregamentoCapacidades.Carregando;
        Mensagem = null;
        Alterado?.Invoke();
        try
        {
            using var response = await http.GetAsync("api/configuracoes/capacidades");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                Estado = EstadoCarregamentoCapacidades.SessaoInvalida;
                Mensagem = "Sua sessão não está mais válida.";
                return;
            }

            var envelope = await response.Content.ReadFromJsonAsync<RespostaApi<SnapshotCapacidadesEmpresaResponse>>();
            if (!response.IsSuccessStatusCode || envelope is not { Sucesso: true, Resultado: not null })
            {
                Estado = EstadoCarregamentoCapacidades.FalhaRede;
                Mensagem = envelope?.Info ?? "Não foi possível carregar os módulos da empresa.";
                return;
            }

            _snapshot = envelope.Resultado;
            Estado = EstadoCarregamentoCapacidades.Carregado;
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            Estado = EstadoCarregamentoCapacidades.FalhaRede;
            Mensagem = "Não foi possível carregar os módulos da empresa.";
        }
        finally
        {
            if (Estado != EstadoCarregamentoCapacidades.Carregado) _carregamento = null;
            Alterado?.Invoke();
        }
    }
}
