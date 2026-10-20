using Detara.Contracts.Financeiro;

namespace Detara.Contracts.Atendimento;

public sealed record RegistrarAtendimentoRapidoRequest(Guid SolicitacaoId, Guid ClienteId,
    Guid? VeiculoId, TipoItemOrcamentoContrato TipoItem, Guid ItemCatalogoId,
    decimal ValorPago, FormaPagamentoContrato FormaPagamento, DateTime RealizadoEmLocal,
    decimal Taxa = 0, int? NumeroParcelas = null, string? ObservacaoPagamento = null);

public sealed record AtendimentoRapidoResponse(Guid OrdemServicoId, string Codigo, Guid ContaReceberId);
public sealed record ContextoAtendimentoRapidoResponse(DateTime AgoraLocal);
