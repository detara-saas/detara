namespace Detara.Contracts.Assinaturas;

public sealed record AssinaturaEmpresaResponse(
    bool PossuiAssinatura,
    Guid? Id,
    string? Status,
    decimal? ValorMensal,
    DateOnly? InicioTeste,
    DateOnly? FimTeste,
    DateOnly? DataConfirmacaoComercial,
    DateTime? ConfirmacaoComercialRegistradaEmUtc,
    int? DiaVencimento,
    DateOnly? PrimeiroVencimento,
    DateOnly? ProximoVencimento,
    long? Versao,
    string? AsaasCustomerId,
    string? AsaasSubscriptionId,
    TermoAceitoResponse? TermoAceito);

public sealed record TermoAceitoResponse(
    Guid Id,
    string VersaoTermo,
    DateTime AceitoEmUtc,
    string HashSha256,
    string ResponsavelNome,
    string ResponsavelEmail);

public sealed record AceitarTermoAssinaturaRequest(bool Aceito);

public sealed record CriarAssinaturaPlataformaRequest(
    decimal ValorMensal,
    DateOnly InicioTeste,
    int DiaVencimento,
    string? AsaasCustomerId,
    string? AsaasSubscriptionId);

public sealed record ConfirmarComercialmenteAssinaturaRequest(
    DateOnly DataConfirmacaoComercial,
    long Versao,
    string Motivo);

public sealed record AlterarCondicoesAssinaturaRequest(
    decimal ValorMensal,
    DateOnly ProximoVencimento,
    int DiaVencimento,
    string? AsaasCustomerId,
    string? AsaasSubscriptionId,
    long Versao,
    string Motivo);

public sealed record AlterarStatusAssinaturaRequest(long Versao, string Motivo);

public sealed record ConfirmarPagamentoAssinaturaRequest(
    DateOnly DataPagamento,
    string? ReferenciaPagamento,
    string? AsaasCustomerId,
    string? AsaasSubscriptionId,
    long Versao,
    string Motivo);

public sealed record HistoricoAssinaturaResponse(
    Guid Id,
    string TipoEvento,
    string? StatusAnterior,
    string StatusNovo,
    DateTime OcorridoEmUtc,
    string Motivo,
    string? Responsavel,
    string? ReferenciaPagamento);

public sealed record AssinaturaPlataformaResponse(
    Guid EmpresaId,
    string EmpresaNome,
    AssinaturaEmpresaResponse Assinatura,
    IReadOnlyCollection<HistoricoAssinaturaResponse> Historico);
