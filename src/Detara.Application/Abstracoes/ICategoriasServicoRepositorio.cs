using Detara.Application.Catalogo;
using Detara.Domain.Entidades;

namespace Detara.Application.Abstracoes;

public interface ICategoriasServicoRepositorio
{
    Task<IReadOnlyCollection<CategoriaServicoResultado>> ListarAsync(bool? ehAtivo, CancellationToken cancellationToken);
    Task<CategoriaServico?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> NomeEmUsoAsync(string nome, Guid? ignorarId, CancellationToken cancellationToken);
    Task<bool> PertenceAoTenantEAtivaAsync(Guid id, Guid empresaId, CancellationToken cancellationToken);
    void Adicionar(CategoriaServico categoria);
    Task SalvarAsync(CancellationToken cancellationToken);
}
