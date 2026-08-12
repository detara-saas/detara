using MudBlazor;

namespace Detara.Web.Servicos;

public interface IMensagemServico
{
    void Mostrar(string mensagem, bool ehErro = false);
}

internal sealed class MensagemServico(ISnackbar snackbar) : IMensagemServico
{
    public void Mostrar(string mensagem, bool ehErro = false) =>
        snackbar.Add(mensagem, ehErro ? Severity.Error : Severity.Success);
}
