namespace Detara.Application.Relatorios;

public static class InsightsRelatorio
{
    public static IReadOnlyList<InsightRelatorio> Criar(FinanceiroRelatorio? f, FinanceiroRelatorio? anterior,
        AtendimentoRelatorio? a, AtendimentoRelatorio? atendimentoAnterior)
    {
        var itens = new List<InsightRelatorio>();
        if (f?.Resultado < 0) itens.Add(new("resultado-negativo", "Despesas acima dos recebimentos", -f.Resultado));
        else if (f is { Recebimentos: >= 3 } && anterior is { Recebimentos: >= 3, Receita: > 0 })
            itens.Add(new("receita", "Receita recebida", f.Receita, PeriodosRelatorio.Variacao(f.Receita, anterior.Receita)));
        if (a is { Concluidos: >= 3 } && atendimentoAnterior is { Concluidos: >= 3, Ticket: > 0 })
            itens.Add(new("ticket", "Ticket médio", a.Ticket, PeriodosRelatorio.Variacao(a.Ticket, atendimentoAnterior.Ticket)));
        if (a?.ServicosQuantidade.FirstOrDefault() is { Quantidade: >= 3 } s)
            itens.Add(new("servico", s.Nome, s.Quantidade));
        if (a is { Clientes: >= 3 }) itens.Add(new("recorrencia", "Clientes recorrentes", a.Recorrentes, decimal.Round(a.Recorrentes * 100m / a.Clientes, 1)));
        if (f is { Despesas: > 0 } && f.Categorias.FirstOrDefault() is { } c)
            itens.Add(new("categoria", c.Nome, c.Valor, decimal.Round(c.Valor * 100m / f.Despesas, 1)));
        if (a is { Concluidos: >= 7 })
        {
            var dia = a.Dias.GroupBy(d => ((int)d.Data.DayOfWeek + 6) % 7)
                .Select(g => new { Dia = g.Key, Quantidade = g.Sum(x => x.Atendimentos) })
                .OrderByDescending(x => x.Quantidade).ThenBy(x => x.Dia).First();
            itens.Add(new("dia", new[] { "Segunda-feira", "Terça-feira", "Quarta-feira", "Quinta-feira", "Sexta-feira", "Sábado", "Domingo" }[dia.Dia],
                dia.Quantidade, decimal.Round(dia.Quantidade * 100m / a.Concluidos, 1)));
        }
        return itens.Take(5).ToArray();
    }
}
