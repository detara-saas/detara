using Detara.Application.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Plataforma;

internal sealed class PlataformaAtendimentoConsulta(DetaraDbContext db) : IPlataformaAtendimentoConsulta
{
    public Task<EmpresaAtendimentoInterno?> ObterEmpresaAsync(Guid empresaId, CancellationToken ct) => db.Empresas.AsNoTracking()
        .Where(x => x.Id == empresaId).Select(x => new EmpresaAtendimentoInterno(x.Id, x.NomeFantasia, x.RazaoSocial,
            x.CpfCnpj, x.Email, x.Telefone, x.FusoHorario)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyDictionary<Guid, string>> ObterNomesUsuariosAsync(Guid empresaId, IReadOnlyCollection<Guid> usuarioIds, CancellationToken ct) =>
        await db.Usuarios.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && usuarioIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.Nome, ct);
}
