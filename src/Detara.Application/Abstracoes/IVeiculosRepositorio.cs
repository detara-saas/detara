using Detara.Application.Veiculos;
using Detara.Domain.Entidades;

namespace Detara.Application.Abstracoes;

public interface IVeiculosRepositorio
{
    Task<PaginacaoResultado<VeiculoListaItemResultado>> ListarAsync(
        FiltroVeiculos filtro,
        CancellationToken cancellationToken);

    Task<VeiculoDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken cancellationToken);
    Task<Veiculo?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> PlacaEmUsoAsync(
        string placa,
        Guid? ignorarVeiculoId,
        CancellationToken cancellationToken);

    void Adicionar(Veiculo veiculo);
    Task SalvarAsync(CancellationToken cancellationToken);
}
