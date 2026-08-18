using Detara.Domain.Clientes;
using Detara.Domain.Entidades;

namespace Detara.Application.Clientes;

public interface IVeiculoFotosRepositorio
{
    Task<Veiculo?> ObterVeiculoAsync(Guid veiculoId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<VeiculoFoto>> ListarAsync(Guid veiculoId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<VeiculoFoto>> ListarParaAlteracaoAsync(Guid veiculoId, CancellationToken cancellationToken);
    Task<VeiculoFoto?> ObterAsync(Guid veiculoId, Guid fotoId, bool paraAlteracao, CancellationToken cancellationToken);
    void Adicionar(VeiculoFoto foto);
    void Remover(VeiculoFoto foto);
    Task SalvarAsync(CancellationToken cancellationToken);
}
