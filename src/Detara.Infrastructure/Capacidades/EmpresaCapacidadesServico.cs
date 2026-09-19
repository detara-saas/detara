using Detara.Application.Abstracoes;
using Detara.Application.Capacidades;
using Detara.Domain.Capacidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Capacidades;

internal sealed class EmpresaCapacidadesServico(
    DetaraDbContext db,
    IUsuarioContexto usuarioContexto) : IEmpresaCapacidadesServico
{
    private Task<SnapshotCapacidadesEmpresa>? _snapshot;

    public Task<SnapshotCapacidadesEmpresa> ObterSnapshotAsync(
        CancellationToken cancellationToken = default)
    {
        if (!usuarioContexto.EstaAutenticado || usuarioContexto.EmpresaId == Guid.Empty)
            throw new InvalidOperationException("Uma empresa autenticada é necessária para consultar capacidades.");

        return _snapshot ??= CarregarAsync(cancellationToken);
    }

    public async Task<bool> PossuiAsync(string codigo, CancellationToken cancellationToken = default)
    {
        if (!CatalogoCapacidadesEmpresa.TentarObter(codigo, out _)) return false;
        return (await ObterSnapshotAsync(cancellationToken)).Possui(codigo);
    }

    public void ValidarConfiguracao(IReadOnlyDictionary<string, bool> capacidades) =>
        CatalogoCapacidadesEmpresa.ValidarConfiguracao(capacidades);

    public IReadOnlyCollection<EmpresaCapacidade> CriarPreset(Guid empresaId, string segmentoCodigo) =>
        PresetCapacidadesEmpresa.Criar(empresaId, segmentoCodigo);

    private async Task<SnapshotCapacidadesEmpresa> CarregarAsync(CancellationToken cancellationToken)
    {
        var segmento = await db.Empresas.AsNoTracking()
            .Where(empresa => empresa.Id == usuarioContexto.EmpresaId)
            .Select(empresa => empresa.SegmentoCodigo)
            .SingleAsync(cancellationToken);
        var persistidas = await db.EmpresasCapacidades.AsNoTracking()
            .ToDictionaryAsync(item => item.Codigo, item => item.Habilitada, StringComparer.Ordinal, cancellationToken);

        var itens = CatalogoCapacidadesEmpresa.Todas
            .OrderBy(item => item.Ordem)
            .Select(definicao => new CapacidadeEmpresaSnapshotItem(
                definicao.Codigo,
                definicao.Nome,
                definicao.Categoria,
                persistidas.TryGetValue(definicao.Codigo, out var habilitada) && habilitada,
                definicao.Configuravel,
                definicao.Ordem))
            .ToArray();
        return new SnapshotCapacidadesEmpresa(segmento, itens);
    }
}
