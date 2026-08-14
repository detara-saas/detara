using Detara.Contracts.Catalogo;

namespace Detara.Web.Servicos;

public static class FormatacaoCatalogo
{
    public static string Preco(TipoPrecificacaoCatalogo tipo, decimal? valor) => tipo switch
    {
        TipoPrecificacaoCatalogo.Fixo when valor.HasValue => valor.Value.ToString("C2"),
        TipoPrecificacaoCatalogo.APartirDe when valor.HasValue => $"A partir de {valor.Value:C2}",
        _ => "Sob consulta"
    };
}
