using Detara.Contracts.Agenda;
using Detara.Contracts.Catalogo;

namespace Detara.Web.Servicos;

public static class FormatacaoCatalogo
{
    public static string Preco(TipoPrecificacaoCatalogo tipo, decimal? valor) => tipo switch
    {
        TipoPrecificacaoCatalogo.Fixo when valor.HasValue => FormatacaoMoeda.Brl(valor.Value),
        TipoPrecificacaoCatalogo.APartirDe when valor.HasValue => $"A partir de {FormatacaoMoeda.Brl(valor.Value)}",
        _ => "Sob consulta"
    };

    public static string Referencia(ResumoReferenciaAgendamentoResponse referencia) => referencia switch
    {
        { PossuiSobConsulta: true } => "Itens sujeitos à avaliação",
        { SomaReferencias: { } valor, PossuiAPartirDe: true } => $"A partir de {FormatacaoMoeda.Brl(valor)}",
        { SomaReferencias: { } valor } => FormatacaoMoeda.Brl(valor),
        _ => "Sob consulta"
    };
}
