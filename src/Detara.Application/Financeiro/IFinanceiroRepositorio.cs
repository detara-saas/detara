using Detara.Application.Abstracoes;
using Detara.Domain.Financeiro;

namespace Detara.Application.Financeiro;

public sealed record FiltroContasReceber(int Pagina, int TamanhoPagina, StatusContaReceber? Status,
    bool? Vencida, DateOnly? CompetenciaInicial, DateOnly? CompetenciaFinal, string? Pesquisa, DateOnly HojeLocal);

public sealed record ContaReceberListaResultado(Guid Id, Guid OrdemServicoId, string OrdemServicoCodigo,
    string ClienteNome, string VeiculoDescricao, string? VeiculoPlaca, DateOnly DataCompetencia,
    DateOnly DataVencimento, decimal ValorOriginal, decimal ValorRecebido, StatusContaReceber Status,
    bool Vencida);

public sealed record ResumoFinanceiroResultado(decimal Faturado, int QuantidadeContas,
    decimal RecebidoBruto, decimal Taxas, decimal EmAbertoAtual, decimal VencidoAtual,
    IReadOnlyCollection<FormaPagamentoResumo> FormasPagamento);

public sealed record FormaPagamentoResumo(FormaPagamento Forma, decimal Valor, int Quantidade);

public interface IFinanceiroRepositorio
{
    Task<PaginacaoResultado<ContaReceberListaResultado>> ListarAsync(FiltroContasReceber filtro, CancellationToken cancellationToken);
    Task<ContaReceber?> ObterAsync(Guid id, bool paraAlteracao, CancellationToken cancellationToken);
    Task<bool> ExistePorOrdemServicoAsync(Guid ordemServicoId, CancellationToken cancellationToken);
    Task<Guid?> ObterIdPorOrdemServicoAsync(Guid ordemServicoId, CancellationToken cancellationToken);
    Task<ResumoFinanceiroResultado> ObterResumoAsync(DateOnly inicio, DateOnly fim, DateTime inicioUtc,
        DateTime fimExclusivoUtc, DateOnly hojeLocal, CancellationToken cancellationToken);
    void Adicionar(ContaReceber conta);
    void AdicionarPagamento(Pagamento pagamento);
    Task SalvarAsync(CancellationToken cancellationToken);
}

public interface IPlataformaFinanceiroConsulta
{
    Task<string?> ObterFusoHorarioAsync(Guid empresaId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> ObterNomesUsuariosAsync(Guid empresaId,
        IReadOnlyCollection<Guid> usuarioIds, CancellationToken cancellationToken);
}

public sealed record OrdemServicoFinalizadaFinanceiro(Guid EmpresaId, Guid OrdemServicoId,
    string OrdemServicoCodigo, Guid ClienteId, string ClienteNome, Guid VeiculoId,
    string VeiculoDescricao, string? VeiculoPlaca, decimal SubtotalAutorizado,
    decimal DescontoAutorizado, decimal AcrescimoAutorizado, decimal TotalAutorizado,
    DateTime FinalizadaEmUtc);

public interface IIntegracaoFinanceiroOrdensServico
{
    Task PrepararContaReceberAsync(OrdemServicoFinalizadaFinanceiro evento, CancellationToken cancellationToken);
}
