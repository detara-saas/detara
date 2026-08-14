using Detara.Application.Clientes;
using Detara.Domain.Entidades;

namespace Detara.Application.Abstracoes;

public interface IClientesRepositorio
{
    Task<PaginacaoResultado<ClienteListaItemResultado>> ListarAsync(
        FiltroClientes filtro,
        CancellationToken cancellationToken);

    Task<IReadOnlyCollection<ClienteBuscaResultado>> BuscarAsync(
        string pesquisa,
        int limite,
        CancellationToken cancellationToken);

    Task<ClienteDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken cancellationToken);
    Task<Cliente?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> DocumentoEmUsoAsync(
        string documento,
        Guid? ignorarClienteId,
        CancellationToken cancellationToken);

    Task<bool> PertenceAoTenantEAtivoAsync(
        Guid clienteId,
        Guid empresaId,
        CancellationToken cancellationToken);

    void Adicionar(Cliente cliente);
    Task SalvarAsync(CancellationToken cancellationToken);
}
