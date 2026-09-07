using Detara.Contracts.Comum;

namespace Detara.Contracts.Financeiro;

public enum StatusDespesaContrato { Pendente = 1, Pago = 2, Cancelado = 3, Vencido = 4 }
public enum OrigemDespesaContrato { Avulsa = 1, Recorrente = 2 }
public sealed record SalvarDespesaRequest(string Descricao, Guid CategoriaId, decimal Valor, DateOnly Competencia,
    DateOnly Vencimento, string? Fornecedor, string? Observacao, long Versao = 0);
public sealed record SalvarRecorrenciaRequest(string Descricao, Guid CategoriaId, decimal Valor, int DiaVencimento,
    DateOnly CompetenciaInicial, DateOnly? CompetenciaFinal, string? Fornecedor, string? Observacao, long Versao = 0);
public sealed record PagarDespesaRequest(DateOnly DataPagamento, decimal ValorPago, long Versao);
public sealed record EstornarDespesaRequest(string Motivo, long Versao);
public sealed record VersaoDespesaRequest(long Versao);
public sealed record AtividadeRecorrenciaRequest(bool Ativa, long Versao);
public sealed record CategoriaDespesaRequest(string Nome, bool Ativa = true, long Versao = 0);
public sealed record DespesaIdResponse(Guid Id);
public sealed record DespesaResponse(Guid Id, string Descricao, Guid CategoriaId, string Categoria,
    OrigemDespesaContrato Origem, Guid? RecorrenciaId, DateOnly Competencia, DateOnly Vencimento, decimal Valor,
    StatusDespesaContrato Status, bool Vencida, DateOnly? DataPagamento, decimal? ValorPago,
    string? Fornecedor, string? Observacao, long Versao);
public sealed record PagamentoDespesaResponse(Guid Id, DateOnly DataPagamento, decimal Valor,
    Guid RegistradoPorUsuarioId, DateTime RegistradoEmUtc, bool Estornado, string? MotivoEstorno,
    Guid? EstornadoPorUsuarioId, DateTime? EstornadoEmUtc);
public sealed record DespesaDetalheResponse(DespesaResponse Conta, IReadOnlyCollection<PagamentoDespesaResponse> Pagamentos);
public sealed record ResumoDespesasResponse(decimal Total, decimal Pago, decimal APagar, decimal Vencido);
public sealed record DespesasResponse(PaginaResponse<DespesaResponse> Contas, ResumoDespesasResponse Resumo,
    DateOnly Competencia, DateOnly Hoje);
public sealed record RecorrenciaDespesaResponse(Guid Id, string Descricao, Guid CategoriaId, string Categoria,
    decimal Valor, int DiaVencimento, DateOnly CompetenciaInicial, DateOnly? CompetenciaFinal,
    DateOnly ProximaCompetencia, bool Ativa, string? Fornecedor, string? Observacao, long Versao);
public sealed record CategoriaDespesaResponse(Guid Id, string Nome, bool Ativa, long Versao);
