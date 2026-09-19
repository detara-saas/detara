using Detara.Domain.Capacidades;

namespace Detara.Application.Capacidades;

public sealed record CapacidadeEmpresaSnapshotItem(
    string Codigo,
    string Nome,
    string Categoria,
    bool Habilitada,
    bool Configuravel,
    int Ordem);

public sealed record SnapshotCapacidadesEmpresa(
    string Segmento,
    IReadOnlyCollection<CapacidadeEmpresaSnapshotItem> Capacidades)
{
    public bool Possui(string codigo) =>
        Capacidades.Any(item => item.Codigo == codigo && item.Habilitada);
}

public interface IEmpresaCapacidadesServico
{
    Task<SnapshotCapacidadesEmpresa> ObterSnapshotAsync(CancellationToken cancellationToken = default);
    Task<bool> PossuiAsync(string codigo, CancellationToken cancellationToken = default);
    void ValidarConfiguracao(IReadOnlyDictionary<string, bool> capacidades);
    IReadOnlyCollection<EmpresaCapacidade> CriarPreset(Guid empresaId, string segmentoCodigo);
}
