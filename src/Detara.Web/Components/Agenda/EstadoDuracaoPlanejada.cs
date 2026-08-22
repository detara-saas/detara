namespace Detara.Web.Components.Agenda;

internal sealed class EstadoDuracaoPlanejada
{
    public int? Valor { get; private set; }
    public bool Personalizada { get; private set; }

    public void InicializarExistente(int valor)
    {
        Valor = valor;
        Personalizada = true;
    }

    public void AtualizarSugestao(int? sugestao, bool possuiItens)
    {
        if (Personalizada)
        {
            return;
        }

        if (!possuiItens)
        {
            Valor = null;
            return;
        }

        if (sugestao.HasValue)
        {
            Valor = sugestao;
        }
    }

    public void Personalizar(int? valor)
    {
        Valor = valor;
        Personalizada = true;
    }

    public void UsarSugestao(int? sugestao)
    {
        if (!sugestao.HasValue)
        {
            return;
        }

        Valor = sugestao;
        Personalizada = false;
    }
}
