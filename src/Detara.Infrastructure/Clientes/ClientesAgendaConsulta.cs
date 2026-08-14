using Detara.Application.Agenda;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Clientes;

internal sealed class ClientesAgendaConsulta(DetaraDbContext db) : IClientesAgendaConsulta
{
    public async Task<ClienteVeiculoAgendaInterno?> ObterClienteVeiculoAsync(Guid empresaId, Guid clienteId, Guid veiculoId, CancellationToken ct)
    {
        var cliente = await db.Clientes.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.Id == clienteId).Select(x => new ClienteAgendaInterno(x.Id, x.Nome, x.Telefone, x.EhAtivo)).SingleOrDefaultAsync(ct);
        if (cliente is null) return null;
        var veiculo = await db.Veiculos.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.Id == veiculoId).Select(x => new VeiculoAgendaInterno(x.Id, x.ClienteId, x.Marca + " " + x.Modelo, x.Placa, x.EhAtivo)).SingleOrDefaultAsync(ct);
        return veiculo is null ? null : new(cliente, veiculo);
    }

    public async Task<IReadOnlyCollection<ClienteAgendaInterno>> BuscarClientesAsync(Guid empresaId, string pesquisa, int limite, CancellationToken ct)
    {
        var digitos = new string(pesquisa.Where(char.IsAsciiDigit).ToArray());
        return await db.Clientes.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.EhAtivo && (x.Nome.Contains(pesquisa) || digitos.Length > 0 && x.Telefone != null && x.Telefone.Contains(digitos))).OrderBy(x => x.Nome).Take(limite).Select(x => new ClienteAgendaInterno(x.Id, x.Nome, x.Telefone, x.EhAtivo)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<VeiculoAgendaInterno>> ListarVeiculosAsync(Guid empresaId, Guid clienteId, bool incluirInativos, CancellationToken ct) => await db.Veiculos.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.ClienteId == clienteId && (incluirInativos || x.EhAtivo)).OrderBy(x => x.Marca).ThenBy(x => x.Modelo).Select(x => new VeiculoAgendaInterno(x.Id, x.ClienteId, x.Marca + " " + x.Modelo, x.Placa, x.EhAtivo)).ToArrayAsync(ct);
}
