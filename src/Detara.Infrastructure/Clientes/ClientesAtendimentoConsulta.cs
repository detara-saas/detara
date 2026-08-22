using Detara.Application.Atendimento;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Clientes;

internal sealed class ClientesAtendimentoConsulta(DetaraDbContext db) : IClientesAtendimentoConsulta
{
    public async Task<ClienteVeiculoAtendimentoInterno?> ObterClienteVeiculoAsync(Guid empresaId, Guid clienteId, Guid veiculoId, CancellationToken ct)
    {
        var cliente = await db.Clientes.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.Id == clienteId)
            .Select(x => new ClienteAtendimentoInterno(x.Id, x.Nome, x.CpfCnpj, x.Telefone ?? x.WhatsApp, x.EhAtivo)).SingleOrDefaultAsync(ct);
        if (cliente is null) return null;
        var veiculo = await db.Veiculos.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.Id == veiculoId)
            .Select(x => new VeiculoAtendimentoInterno(x.Id, x.ClienteId,
                x.Marca + " " + x.Modelo + (x.Placa != null ? " · " + x.Placa :
                    x.IdentificacaoAlternativa != null ? " · " + x.IdentificacaoAlternativa : ""),
                x.Placa, x.EhAtivo)).SingleOrDefaultAsync(ct);
        return veiculo is null ? null : new(cliente, veiculo);
    }

    public async Task<IReadOnlyCollection<ClienteAtendimentoInterno>> BuscarClientesAsync(Guid empresaId, string pesquisa, int limite, CancellationToken ct)
    {
        var termo = pesquisa.Trim(); var digitos = new string(termo.Where(char.IsAsciiDigit).ToArray());
        return await Projetar(db.Clientes.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.EhAtivo
            && (x.Nome.Contains(termo) || digitos.Length > 0 && x.CpfCnpj != null && x.CpfCnpj.Contains(digitos)
                || digitos.Length > 0 && x.Telefone != null && x.Telefone.Contains(digitos))).OrderBy(x => x.Nome).Take(limite)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyCollection<VeiculoAtendimentoInterno>> ListarVeiculosAsync(Guid empresaId, Guid clienteId, CancellationToken ct) =>
        await db.Veiculos.IgnoreQueryFilters().AsNoTracking().Where(x => x.EmpresaId == empresaId && x.ClienteId == clienteId && x.EhAtivo)
            .OrderBy(x => x.Marca).ThenBy(x => x.Modelo).Select(x => new VeiculoAtendimentoInterno(x.Id, x.ClienteId,
                x.Marca + " " + x.Modelo + (x.Placa != null ? " · " + x.Placa :
                    x.IdentificacaoAlternativa != null ? " · " + x.IdentificacaoAlternativa : ""),
                x.Placa, x.EhAtivo)).ToArrayAsync(ct);

    private static IQueryable<ClienteAtendimentoInterno> Projetar(IQueryable<Detara.Domain.Entidades.Cliente> query) =>
        query.Select(x => new ClienteAtendimentoInterno(x.Id, x.Nome, x.CpfCnpj, x.Telefone ?? x.WhatsApp, x.EhAtivo));
}
