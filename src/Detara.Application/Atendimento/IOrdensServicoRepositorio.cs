using Detara.Application.Abstracoes;
using Detara.Domain.Atendimento;

namespace Detara.Application.Atendimento;

public sealed record FiltroOrdensServico(int Pagina, int TamanhoPagina, StatusOrdemServico? Status,
    DateOnly? DataInicial, DateOnly? DataFinal, string? Pesquisa);
public sealed record OrdemServicoListaResultado(Guid Id, string Codigo, string ClienteNome, string VeiculoDescricao,
    string VeiculoPlaca, StatusOrdemServico Status, decimal TotalAutorizado, DateTime CriadoEmUtc);

public interface IOrdensServicoRepositorio
{
    Task<PaginacaoResultado<OrdemServicoListaResultado>> ListarAsync(FiltroOrdensServico filtro, CancellationToken cancellationToken);
    Task<OrdemServico?> ObterAsync(Guid id, bool paraAlteracao, CancellationToken cancellationToken);
    Task<bool> ExistePorOrcamentoAsync(Guid orcamentoId, CancellationToken cancellationToken);
    Task<OrdemServico?> ObterPorOrcamentoAdicionalAsync(Guid orcamentoId, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<Orcamento>> ListarOrcamentosAdicionaisAsync(Guid ordemServicoId, CancellationToken cancellationToken);
    void Adicionar(OrdemServico ordemServico);
    void AdicionarChecklist(OrdemServicoChecklist checklist);
    void AdicionarItens(IReadOnlyCollection<OrdemServicoItem> itens);
    void AdicionarUltimoHistorico(OrdemServico ordemServico);
    void AdicionarFoto(OrdemServicoFoto foto);
    void RemoverFoto(OrdemServicoFoto foto);
    Task SalvarAsync(CancellationToken cancellationToken);
}
