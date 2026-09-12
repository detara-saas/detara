using Detara.Application.Catalogo;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Atendimento;

internal sealed class HistoricoExecucoesCatalogoConsulta(DetaraDbContext db)
    : IHistoricoExecucoesCatalogoConsulta
{
    public async Task<IReadOnlyCollection<ExecucaoItemCatalogoResultado>> ListarAsync(Guid empresaId,
        TipoItemOrcamento tipoItem, Guid itemCatalogoId, int limite, CancellationToken ct)
    {
        if (empresaId == Guid.Empty || itemCatalogoId == Guid.Empty || limite is < 1 or > 10)
            return [];

        return await db.OrdensServicoItens.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.EmpresaId == empresaId && item.OrdemServico.EmpresaId == empresaId &&
                item.TipoItem == tipoItem && item.ItemCatalogoId == itemCatalogoId &&
                item.OrdemServico.ExecucaoFinalizadaEmUtc != null &&
                (item.OrdemServico.Status == StatusOrdemServico.AguardandoRetirada ||
                 item.OrdemServico.Status == StatusOrdemServico.Concluida))
            .OrderByDescending(item => item.OrdemServico.ExecucaoFinalizadaEmUtc)
            .ThenByDescending(item => item.Id)
            .Select(item => new ExecucaoItemCatalogoResultado(item.OrdemServicoId,
                item.OrdemServico.Codigo, item.OrdemServico.ExecucaoFinalizadaEmUtc!.Value,
                item.OrdemServico.ClienteNomeSnapshot, item.OrdemServico.VeiculoDescricaoSnapshot,
                item.OrdemServico.VeiculoPlacaSnapshot, item.ValorUnitarioAutorizado,
                item.Quantidade, item.OrdemServico.Status))
            .Take(limite)
            .ToArrayAsync(ct);
    }
}
