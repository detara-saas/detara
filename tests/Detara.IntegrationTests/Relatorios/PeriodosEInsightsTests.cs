using Detara.Application.Abstracoes;
using Detara.Application.Relatorios;
using Detara.Application.Agenda;

namespace Detara.IntegrationTests.Relatorios;

public sealed class PeriodosEInsightsTests
{
    [Theory]
    [InlineData(PeriodoRelatorio.Hoje, "2026-09-07", "2026-09-07", "2026-09-06", "2026-09-06")]
    [InlineData(PeriodoRelatorio.Ultimos7Dias, "2026-09-01", "2026-09-07", "2026-08-25", "2026-08-31")]
    [InlineData(PeriodoRelatorio.Ultimos30Dias, "2026-08-09", "2026-09-07", "2026-07-10", "2026-08-08")]
    [InlineData(PeriodoRelatorio.EsteMes, "2026-09-01", "2026-09-07", "2026-08-01", "2026-08-07")]
    [InlineData(PeriodoRelatorio.MesAnterior, "2026-08-01", "2026-08-31", "2026-07-01", "2026-07-31")]
    [InlineData(PeriodoRelatorio.EsteAno, "2026-01-01", "2026-09-07", "2025-01-01", "2025-09-07")]
    [InlineData(PeriodoRelatorio.Personalizado, "2026-08-31", "2026-09-02", "2026-08-28", "2026-08-30")]
    public void Periodos_EComparacao(PeriodoRelatorio tipo, string inicio, string fim, string anteriorInicio, string anteriorFim)
    {
        var p = PeriodosRelatorio.Resolver(tipo, new(2026, 8, 31), new(2026, 9, 2), new(2026, 9, 7), "America/Sao_Paulo", new ConversorFusoHorario());
        Assert.Equal(DateOnly.Parse(inicio), p.Atual.Inicio); Assert.Equal(DateOnly.Parse(fim), p.Atual.Fim);
        Assert.Equal(DateOnly.Parse(anteriorInicio), p.Anterior.Inicio); Assert.Equal(DateOnly.Parse(anteriorFim), p.Anterior.Fim);
        Assert.Equal(p.Atual.Fim.AddDays(1).ToDateTime(new TimeOnly(3, 0)), p.Atual.FimExclusivoUtc);
    }
    [Theory]
    [InlineData("2026-03-31", PeriodoRelatorio.EsteMes, "2026-02-28")]
    [InlineData("2024-02-29", PeriodoRelatorio.EsteAno, "2023-02-28")]
    [InlineData("2026-01-01", PeriodoRelatorio.MesAnterior, "2025-11-30")]
    public void Calendario_LimitesReais(string hoje, PeriodoRelatorio tipo, string anteriorFim)
    {
        var p = PeriodosRelatorio.Resolver(tipo, null, null, DateOnly.Parse(hoje), "America/Sao_Paulo", new ConversorFusoHorario());
        Assert.Equal(DateOnly.Parse(anteriorFim), p.Anterior.Fim);
    }
    [Fact]
    public void DiaComHorarioDeVerao_NaoPressupoe24Horas()
    {
        var p = PeriodosRelatorio.Resolver(PeriodoRelatorio.Hoje, null, null, new(2026, 3, 8), "America/New_York", new ConversorFusoHorario());
        Assert.Equal(23, (p.Atual.FimExclusivoUtc - p.Atual.InicioUtc).TotalHours);
    }
    [Theory]
    [InlineData(null, "2026-09-07")]
    [InlineData("2026-09-07", null)]
    [InlineData("2026-09-08", "2026-09-07")]
    [InlineData("1999-01-01", "2026-09-07")]
    [InlineData("2020-01-01", "2026-09-07")]
    [InlineData("9999-01-01", "9999-01-02")]
    public void Personalizado_Invalido(string? inicio, string? fim) => Assert.Throws<ConflitoRegraNegocioException>(() =>
        PeriodosRelatorio.Resolver(PeriodoRelatorio.Personalizado, inicio is null ? null : DateOnly.Parse(inicio), fim is null ? null : DateOnly.Parse(fim), new(2026, 9, 7), "UTC", new ConversorFusoHorario()));
    [Theory]
    [InlineData(110, 100, 10)]
    [InlineData(90, 100, -10)]
    [InlineData(0, 100, -100)]
    public void Variacao(decimal atual, decimal anterior, decimal esperado) => Assert.Equal(esperado, PeriodosRelatorio.Variacao(atual, anterior));
    [Theory]
    [InlineData(100)]
    [InlineData(0)]
    public void SemBase(decimal atual) => Assert.Null(PeriodosRelatorio.Variacao(atual, 0));
    [Theory]
    [InlineData(10000, 4000, 6000)]
    [InlineData(1000, 4000, -3000)]
    public void Resultado(decimal receita, decimal despesa, decimal esperado) => Assert.Equal(esperado, F(receita, despesa).Resultado);
    [Fact]
    public void Insights_VaziosEThresholds()
    {
        Assert.Empty(InsightsRelatorio.Criar(null, null, null, null));
        Assert.Empty(InsightsRelatorio.Criar(F(0, 0), F(0, 0), A(0), A(0)));
        Assert.DoesNotContain(InsightsRelatorio.Criar(F(100, 0), F(0, 0), A(2), A(2)), x => x.Tipo is "ticket" or "receita" or "dia");
    }
    [Theory]
    [InlineData(110, 10)]
    [InlineData(90, -10)]
    public void Insights_TicketEOrdemDeterministica(decimal ticket, decimal variacao)
    {
        var a = A(7) with
        {
            Valor = ticket * 7,
            ServicosQuantidade = [new(Guid.NewGuid(), "Lavagem", 5, 100)],
            Dias = [new(new(2026, 9, 7), 0, 0, 7)]
        };
        var f = F(2000, 1000) with { Categorias = [new(Guid.NewGuid(), "Produtos", 2, 1000)] };
        var itens = InsightsRelatorio.Criar(f, F(1000, 0), a, A(7));
        Assert.Equal(itens, InsightsRelatorio.Criar(f, F(1000, 0), a, A(7)));
        Assert.Equal(variacao, itens.Single(x => x.Tipo == "ticket").Percentual);
        Assert.Equal(new[] { "receita", "ticket", "servico", "recorrencia", "categoria" }, itens.Select(x => x.Tipo));
    }
    [Fact]
    public void Insight_NegativoEDia()
    {
        var a = A(7) with { Dias = [new(new(2026, 9, 7), 0, 0, 7)] };
        var itens = InsightsRelatorio.Criar(F(100, 200), null, a, null);
        Assert.Equal(100, itens.Single(x => x.Tipo == "resultado-negativo").Valor);
        Assert.Equal("Segunda-feira", itens.Single(x => x.Tipo == "dia").Nome);
    }
    [Fact]
    public void Conversao_SomenteDecisoes() { Assert.Equal(80, new OrcamentosRelatorio(18, 8, 2).Conversao); Assert.Null(new OrcamentosRelatorio(5, 0, 0).Conversao); }
    private static FinanceiroRelatorio F(decimal r, decimal d) => new(r, d, 3, 3, 0, 0, 0, [], []);
    private static AtendimentoRelatorio A(int n) => new(n, n * 100, n, n, n, [], [], [], [], []);
}
