using Detara.Application.Abstracoes;
using Detara.Application.Clientes;
using Detara.Domain.Entidades;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Infrastructure.Clientes;

internal sealed class ClientesRepositorio(DetaraDbContext dbContext) : IClientesRepositorio
{
    public async Task<PaginacaoResultado<ClienteListaItemResultado>> ListarAsync(
        FiltroClientes filtro,
        CancellationToken cancellationToken)
    {
        var consulta = dbContext.Clientes.AsNoTracking();
        if (filtro.EhAtivo.HasValue)
        {
            consulta = consulta.Where(item => item.EhAtivo == filtro.EhAtivo.Value);
        }

        if (filtro.TipoPessoa.HasValue)
        {
            consulta = consulta.Where(item => item.TipoPessoa == filtro.TipoPessoa.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Pesquisa))
        {
            var pesquisa = filtro.Pesquisa.Trim();
            var digitos = new string(pesquisa.Where(char.IsAsciiDigit).ToArray());
            var placa = new string(pesquisa.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());
            consulta = consulta.Where(item =>
                item.Nome.Contains(pesquisa) ||
                digitos.Length > 0 && item.CpfCnpj != null && item.CpfCnpj.Contains(digitos) ||
                digitos.Length > 0 && item.Telefone != null && item.Telefone.Contains(digitos) ||
                placa.Length > 0 && item.Veiculos.Any(veiculo =>
                    veiculo.Placa != null && veiculo.Placa.Contains(placa) ||
                    veiculo.IdentificacaoAlternativa != null && veiculo.IdentificacaoAlternativa.Contains(pesquisa) ||
                    veiculo.Marca.Contains(pesquisa) || veiculo.Modelo.Contains(pesquisa)));
        }

        consulta = filtro.Ordenacao == "criacao"
            ? consulta.OrderByDescending(item => item.CriadoEmUtc).ThenBy(item => item.Nome)
            : consulta.OrderBy(item => item.Nome);
        var total = await consulta.CountAsync(cancellationToken);
        var itens = await consulta
            .Skip((filtro.Pagina - 1) * filtro.TamanhoPagina)
            .Take(filtro.TamanhoPagina)
            .Select(item => new ClienteListaItemResultado(
                item.Id,
                item.Nome,
                item.TipoPessoa,
                item.CpfCnpj,
                item.Telefone,
                item.Veiculos.Count,
                item.EhAtivo))
            .ToArrayAsync(cancellationToken);
        return new PaginacaoResultado<ClienteListaItemResultado>(
            itens,
            filtro.Pagina,
            filtro.TamanhoPagina,
            total);
    }

    public async Task<IReadOnlyCollection<ClienteBuscaResultado>> BuscarAsync(
        string pesquisa,
        int limite,
        CancellationToken cancellationToken)
    {
        var digitos = new string(pesquisa.Where(char.IsAsciiDigit).ToArray());
        return await dbContext.Clientes
            .AsNoTracking()
            .Where(item => item.EhAtivo &&
                (item.Nome.Contains(pesquisa) ||
                 digitos.Length > 0 && item.Telefone != null && item.Telefone.Contains(digitos) ||
                 digitos.Length > 0 && item.CpfCnpj != null && item.CpfCnpj.Contains(digitos)))
            .OrderBy(item => item.Nome)
            .Take(limite)
            .Select(item => new ClienteBuscaResultado(item.Id, item.Nome, item.Telefone, item.CpfCnpj))
            .ToArrayAsync(cancellationToken);
    }

    public Task<ClienteDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Clientes
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new ClienteDetalheResultado(
                item.Id,
                item.Nome,
                item.TipoPessoa,
                item.CpfCnpj,
                item.Telefone,
                item.WhatsApp,
                item.Email,
                item.DataNascimento,
                item.Observacao,
                item.CriadoEmUtc,
                item.AtualizadoEmUtc,
                item.EhAtivo,
                item.Veiculos
                    .OrderBy(veiculo => veiculo.Marca)
                    .ThenBy(veiculo => veiculo.Modelo)
                    .Select(veiculo => new VeiculoResumoClienteResultado(
                        veiculo.Id,
                        veiculo.Marca + " " + veiculo.Modelo +
                            (veiculo.Placa != null ? " · " + veiculo.Placa :
                             veiculo.IdentificacaoAlternativa != null ? " · " + veiculo.IdentificacaoAlternativa : ""),
                        veiculo.Tipo,
                        veiculo.Placa,
                        veiculo.IdentificacaoAlternativa,
                        veiculo.AnoModelo,
                        veiculo.Cor,
                        veiculo.Quilometragem,
                        veiculo.EhAtivo))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);

    public Task<Cliente?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Clientes.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);

    public Task<bool> DocumentoEmUsoAsync(
        string documento,
        Guid? ignorarClienteId,
        CancellationToken cancellationToken) =>
        dbContext.Clientes.AnyAsync(
            item => item.CpfCnpj == documento &&
                    (!ignorarClienteId.HasValue || item.Id != ignorarClienteId.Value),
            cancellationToken);

    public Task<bool> PertenceAoTenantEAtivoAsync(
        Guid clienteId,
        Guid empresaId,
        CancellationToken cancellationToken) =>
        dbContext.Clientes
            .IgnoreQueryFilters()
            .AnyAsync(
                item => item.Id == clienteId && item.EmpresaId == empresaId && item.EhAtivo,
                cancellationToken);

    public void Adicionar(Cliente cliente) => dbContext.Clientes.Add(cliente);
    public Task SalvarAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
