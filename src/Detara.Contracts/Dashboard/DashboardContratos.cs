using Detara.Contracts.Agenda;

namespace Detara.Contracts.Dashboard;

public sealed record DashboardOperacionalResponse(
    DateOnly DataReferencia,
    DateTime AtualizadoEmLocal,
    string FusoHorario,
    DashboardAgendaResponse? Agenda,
    DashboardAtendimentoResponse? Atendimento,
    DashboardOrcamentosResponse? Orcamentos,
    DashboardFinanceiroResponse? Financeiro);

public sealed record DashboardAgendaResponse(
    int AgendamentosHoje,
    int ConcluidosHoje,
    IReadOnlyCollection<DashboardAgendamentoResponse> Itens);

public sealed record DashboardAgendamentoResponse(
    Guid Id,
    DateTime InicioLocal,
    string ClienteNome,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    string? ItemPrincipal,
    StatusAgendamentoContrato Status);

public sealed record DashboardAtendimentoResponse(
    int OrdensEmExecucao,
    int OrdensAguardandoRetirada);

public sealed record DashboardOrcamentosResponse(int OrcamentosEmAberto);

public sealed record DashboardFinanceiroResponse(
    DateOnly InicioPeriodo,
    DateOnly FimPeriodo,
    decimal RecebidoBruto,
    decimal Taxas,
    decimal ReceitaLiquidaRecebida,
    int ContasPendentes,
    decimal ValorPendente);
