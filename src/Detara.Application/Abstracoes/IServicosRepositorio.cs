using Detara.Application.Catalogo;
using Detara.Domain.Entidades;

namespace Detara.Application.Abstracoes;

public interface IServicosRepositorio
{
    Task<PaginacaoResultado<ServicoListaItemResultado>> ListarAsync(FiltroServicos filtro, CancellationToken cancellationToken);
    Task<ServicoDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<ServicoSelecaoResultado>> ListarParaSelecaoAsync(bool incluirInativos, CancellationToken cancellationToken);
    Task<Servico?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> NomeEmUsoAsync(Guid categoriaId, string nome, Guid? ignorarId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Guid>> ObterIdsDoTenantAsync(IReadOnlyCollection<Guid> ids, Guid empresaId, CancellationToken cancellationToken);
    void Adicionar(Servico servico);
    Task SalvarAsync(CancellationToken cancellationToken);
}
