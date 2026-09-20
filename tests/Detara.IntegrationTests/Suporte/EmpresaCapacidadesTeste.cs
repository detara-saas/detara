using Detara.Application.Capacidades;
using Detara.Domain.Capacidades;

namespace Detara.IntegrationTests.Suporte;

internal sealed class EmpresaCapacidadesTeste(bool veiculos = true) : IEmpresaCapacidadesServico
{
    public Task<SnapshotCapacidadesEmpresa> ObterSnapshotAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(new SnapshotCapacidadesEmpresa(SegmentosEmpresa.EsteticaAutomotiva,
        [
            new(CodigosCapacidadeEmpresa.Veiculos, "Veículos", "Automotivo", veiculos, false, 90),
            new(CodigosCapacidadeEmpresa.CheckIn, "Check-in", "Automotivo", veiculos, false, 100)
        ]));

    public Task<bool> PossuiAsync(string codigo, CancellationToken cancellationToken = default) =>
        Task.FromResult(codigo is CodigosCapacidadeEmpresa.Veiculos or CodigosCapacidadeEmpresa.CheckIn
            ? veiculos
            : true);

    public void ValidarConfiguracao(IReadOnlyDictionary<string, bool> capacidades) =>
        CatalogoCapacidadesEmpresa.ValidarConfiguracao(capacidades);

    public IReadOnlyCollection<EmpresaCapacidade> CriarPreset(Guid empresaId, string segmentoCodigo) =>
        PresetCapacidadesEmpresa.Criar(empresaId, segmentoCodigo);
}
