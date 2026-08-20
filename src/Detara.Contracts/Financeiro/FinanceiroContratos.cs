namespace Detara.Contracts.Financeiro;

public enum StatusContaReceberContrato
{
    EmAberto = 1,
    ParcialmentePago = 2,
    Pago = 3
}

public enum FormaPagamentoContrato
{
    Pix = 1,
    Dinheiro = 2,
    CartaoDebito = 3,
    CartaoCredito = 4,
    Boleto = 5,
    Transferencia = 6,
    Outro = 7
}

public enum StatusPagamentoContrato
{
    Confirmado = 1,
    Estornado = 2
}

public sealed record ContaReceberListaResponse(Guid Id, Guid OrdemServicoId, string OrdemServicoCodigo,
    string ClienteNome, string VeiculoDescricao, string VeiculoPlaca, DateOnly DataCompetencia,
    DateOnly DataVencimento, decimal ValorOriginal, decimal ValorRecebido, decimal ValorEmAberto,
    StatusContaReceberContrato Status, bool Vencida);

public sealed record PagamentoResponse(Guid Id, FormaPagamentoContrato FormaPagamento, decimal Valor,
    decimal Taxa, decimal ValorLiquido, int? NumeroParcelas, string? Observacao, DateTime RecebidoEmUtc,
    Guid RegistradoPorUsuarioId, string RegistradoPorUsuarioNome, DateTime RegistradoEmUtc,
    StatusPagamentoContrato Status, DateTime? EstornadoEmUtc, Guid? EstornadoPorUsuarioId,
    string? EstornadoPorUsuarioNome, string? MotivoEstorno);

public sealed record ContaReceberDetalheResponse(Guid Id, Guid OrdemServicoId, string OrdemServicoCodigo,
    Guid ClienteId, string ClienteNome, Guid VeiculoId, string VeiculoDescricao, string VeiculoPlaca,
    decimal SubtotalAutorizado, decimal DescontoAutorizado, decimal AcrescimoAutorizado,
    decimal ValorOriginal, decimal ValorRecebido, decimal ValorEmAberto, DateOnly DataCompetencia,
    DateOnly DataVencimento, StatusContaReceberContrato Status, bool Vencida, string FusoHorario,
    DateTime CriadoEmUtc, DateTime? AtualizadoEmUtc, IReadOnlyCollection<PagamentoResponse> Pagamentos);

public sealed record FormaPagamentoResumoResponse(FormaPagamentoContrato FormaPagamento, decimal Valor, int Quantidade);
public sealed record ResumoFinanceiroResponse(DateOnly Inicio, DateOnly Fim, decimal Faturado,
    decimal RecebidoBruto, decimal Taxas, decimal ReceitaLiquidaRecebida, decimal EmAbertoAtual,
    decimal VencidoAtual, decimal TicketMedio, IReadOnlyCollection<FormaPagamentoResumoResponse> FormasPagamento);

public sealed record RegistrarPagamentoRequest(FormaPagamentoContrato FormaPagamento, decimal Valor,
    decimal Taxa, int? NumeroParcelas, string? Observacao, DateTime RecebidoEmLocal);
public sealed record EstornarPagamentoRequest(string Motivo);
public sealed record AlterarVencimentoRequest(DateOnly DataVencimento);
public sealed record ContaReceberVinculoResponse(bool Existe, Guid? Id);
