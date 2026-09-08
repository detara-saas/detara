using System.Globalization;
using Detara.Contracts.Relatorios;

namespace Detara.Web.Components.Relatorios;

public static class ApresentacaoRelatorio
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("pt-BR");
    public static string Moeda(decimal valor) => valor.ToString("C2", Cultura);
    public static string Numero(decimal valor) => valor.ToString("N0", Cultura);
    public static string Percentual(decimal? valor) => valor.HasValue ? valor.Value.ToString("N1", Cultura) + "%" : "Sem base de comparação";
    public static string Coordenada(decimal valor) => valor.ToString("0.##", CultureInfo.InvariantCulture);
    public static string Variacao(decimal? valor) => valor.HasValue ? $"{(valor > 0 ? "+" : "")}{Percentual(valor)} ante o período anterior" : "Sem base de comparação";
    public static string Insight(InsightRelatorioResponse item) => item.Tipo switch
    {
        "resultado-negativo" => $"As despesas pagas superaram as receitas recebidas em {Moeda(item.Valor)} neste período.",
        "receita" => $"Receita recebida: {Variacao(item.Percentual).ToLower(Cultura)}.",
        "ticket" => $"Ticket médio: {Variacao(item.Percentual).ToLower(Cultura)}.",
        "servico" => $"{item.Nome} foi o serviço/item mais realizado, com {Numero(item.Valor)} execuções no período.",
        "recorrencia" => $"{Percentual(item.Percentual)} dos clientes atendidos já haviam concluído um atendimento antes deste período.",
        "categoria" => $"{item.Nome} foi a maior categoria de despesa paga, representando {Percentual(item.Percentual)} do total.",
        "dia" => $"{item.Nome} concentrou {Percentual(item.Percentual)} dos atendimentos concluídos no período.",
        _ => item.Nome
    };
    public static IReadOnlyList<DiaRelatorioResponse> Serie(RelatorioResponse dados)
    {
        if (dados.Financeiro is null || dados.Financeiro.Recebimentos + dados.Financeiro.Pagamentos == 0) return [];
        var mensal = dados.Fim.DayNumber - dados.Inicio.DayNumber > 62;
        DateOnly Chave(DateOnly d) => mensal ? new(d.Year, d.Month, 1) : d;
        var grupos = dados.Financeiro.Serie.GroupBy(x => Chave(x.Data)).ToDictionary(g => g.Key,
            g => new DiaRelatorioResponse(g.Key, g.Sum(x => x.Receita), g.Sum(x => x.Despesa), 0));
        var resultado = new List<DiaRelatorioResponse>();
        for (var d = Chave(dados.Inicio); d <= dados.Fim; d = mensal ? d.AddMonths(1) : d.AddDays(1))
            resultado.Add(grupos.GetValueOrDefault(d) ?? new(d, 0, 0, 0));
        return resultado;
    }
}
