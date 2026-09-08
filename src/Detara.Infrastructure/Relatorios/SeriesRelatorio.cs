using Detara.Application.Relatorios;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Relatorios;

internal sealed class MovimentoRelatorio
{
    public DateTime Data { get; init; }
    public decimal Valor { get; init; }
    public int Quantidade { get; init; }
}

internal static class SeriesRelatorio
{
    // SQL Server agrega no dia civil do tenant. SQLite é usado apenas nos testes:
    // agrega por minuto UTC antes da conversão, preservando offsets não inteiros e DST.
    public static async Task<IReadOnlyList<DiaRelatorio>> AgregarAsync(IQueryable<MovimentoRelatorio> query,
        bool sqlServer, string fuso, CancellationToken ct)
    {
        var zona = TimeZoneInfo.FindSystemTimeZoneById(fuso);
        var sqlZona = TimeZoneInfo.TryConvertIanaIdToWindowsId(fuso, out var windows) ? windows : fuso;
        var localizada = sqlServer
            ? query.Select(x => new MovimentoRelatorio
            {
                Data = EF.Functions.AtTimeZone(EF.Functions.AtTimeZone(x.Data, "UTC"), sqlZona!).Date,
                Valor = x.Valor,
                Quantidade = x.Quantidade
            })
            : query.Select(x => new MovimentoRelatorio { Data = x.Data.Date.AddHours(x.Data.Hour).AddMinutes(x.Data.Minute), Valor = x.Valor, Quantidade = x.Quantidade });
        var agregados = await localizada.GroupBy(x => x.Data)
            .Select(g => new MovimentoRelatorio { Data = g.Key, Valor = g.Sum(x => x.Valor), Quantidade = g.Sum(x => x.Quantidade) })
            .ToArrayAsync(ct);
        return agregados.GroupBy(x => DateOnly.FromDateTime(sqlServer ? x.Data :
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(x.Data, DateTimeKind.Utc), zona)))
            .Select(g => new DiaRelatorio(g.Key, g.Sum(x => x.Valor), 0, g.Sum(x => x.Quantidade)))
            .OrderBy(x => x.Data).ToArray();
    }
}
