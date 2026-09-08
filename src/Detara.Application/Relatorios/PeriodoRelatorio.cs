using Detara.Application.Abstracoes;
using Detara.Application.Agenda;

namespace Detara.Application.Relatorios;

public enum PeriodoRelatorio { Hoje = 1, Ultimos7Dias, Ultimos30Dias, EsteMes, MesAnterior, EsteAno, Personalizado }
public enum PerspectivaRelatorio { Geral = 1, Servicos, Clientes, Financeiro, Operacao }

public sealed record IntervaloRelatorio(DateOnly Inicio, DateOnly Fim, DateTime InicioUtc, DateTime FimExclusivoUtc);
public sealed record PeriodoRelatorioResolvido(IntervaloRelatorio Atual, IntervaloRelatorio Anterior, DateOnly Hoje, string Fuso);

public static class PeriodosRelatorio
{
    public static PeriodoRelatorioResolvido Resolver(PeriodoRelatorio tipo, DateOnly? inicio, DateOnly? fim,
        DateOnly hoje, string fuso, IConversorFusoHorario conversor)
    {
        if (!Enum.IsDefined(tipo)) throw new ConflitoRegraNegocioException("Período inválido.");
        var mes = new DateOnly(hoje.Year, hoje.Month, 1);
        var primeiro = tipo switch
        {
            PeriodoRelatorio.Hoje => hoje,
            PeriodoRelatorio.Ultimos7Dias => hoje.AddDays(-6),
            PeriodoRelatorio.Ultimos30Dias => hoje.AddDays(-29),
            PeriodoRelatorio.EsteMes => mes,
            PeriodoRelatorio.MesAnterior => mes.AddMonths(-1),
            PeriodoRelatorio.EsteAno => new DateOnly(hoje.Year, 1, 1),
            _ => inicio ?? throw new ConflitoRegraNegocioException("Informe a data inicial.")
        };
        var ultimo = tipo == PeriodoRelatorio.Personalizado
            ? fim ?? throw new ConflitoRegraNegocioException("Informe a data final.")
            : tipo == PeriodoRelatorio.MesAnterior ? mes.AddDays(-1) : hoje;
        if (primeiro.Year < 2000 || ultimo.Year > 9998 || ultimo < primeiro || ultimo.DayNumber - primeiro.DayNumber >= 1096)
            throw new ConflitoRegraNegocioException("Informe até três anos de datas válidas, com fim igual ou posterior ao início.");
        var dias = ultimo.DayNumber - primeiro.DayNumber + 1;
        var anteriorInicio = tipo switch
        {
            PeriodoRelatorio.EsteMes or PeriodoRelatorio.MesAnterior => primeiro.AddMonths(-1),
            PeriodoRelatorio.EsteAno => primeiro.AddYears(-1),
            _ => primeiro.AddDays(-dias)
        };
        var anteriorFim = tipo switch
        {
            PeriodoRelatorio.EsteMes => anteriorInicio.AddDays(Math.Min(dias, DateTime.DaysInMonth(anteriorInicio.Year, anteriorInicio.Month)) - 1),
            PeriodoRelatorio.MesAnterior => primeiro.AddDays(-1),
            PeriodoRelatorio.EsteAno => ultimo.AddYears(-1),
            _ => primeiro.AddDays(-1)
        };
        IntervaloRelatorio Criar(DateOnly a, DateOnly b) => new(a, b,
            conversor.ParaUtc(a.ToDateTime(TimeOnly.MinValue), fuso),
            conversor.ParaUtc(b.AddDays(1).ToDateTime(TimeOnly.MinValue), fuso));
        return new(Criar(primeiro, ultimo), Criar(anteriorInicio, anteriorFim), hoje, fuso);
    }

    public static decimal? Variacao(decimal atual, decimal anterior) => anterior == 0
        ? null : decimal.Round((atual - anterior) / Math.Abs(anterior) * 100m, 1);
}
