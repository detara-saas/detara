using Detara.Application.Catalogo;
using Detara.Domain.Entidades;

namespace Detara.Application.Abstracoes;

public interface IPacotesRepositorio
{
    Task<PaginacaoResultado<PacoteListaItemResultado>> ListarAsync(FiltroPacotes filtro, CancellationToken cancellationToken);
    Task<PacoteDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken cancellationToken);
    Task<Pacote?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> NomeEmUsoAsync(string nome, Guid? ignorarId, CancellationToken cancellationToken);
    void Adicionar(Pacote pacote);
    void RemoverComposicaoAtual(Pacote pacote);
    void AdicionarComposicaoAtual(Pacote pacote);
    Task SalvarAsync(CancellationToken cancellationToken);
}
