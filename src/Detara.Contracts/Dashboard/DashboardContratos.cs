using Detara.Contracts.Agenda;

namespace Detara.Contracts.Dashboard;

public enum PeriodoDashboardContrato
{
    Hoje = 1,
    Ultimos7Dias = 2,
    EsteMes = 3,
    EsteAno = 4
}

public enum GranularidadeDashboardContrato { Dia = 1, Mes = 2 }

public enum TipoAtividadeDashboardContrato
{
    AgendamentoCriado = 1,
    OrcamentoAprovado = 2,
    ClienteChegou = 3,
    OrdemServicoIniciada = 4,
    VeiculoEntregue = 5,
    PagamentoRecebido = 6,
    ComunicacaoEnviada = 7
}

public enum TipoAtencaoDashboardContrato
{
    VeiculosAguardandoRetirada = 1,
    OrcamentosAguardandoAprovacao = 2,
    AtendimentosAtrasados = 3,
    PendenciasFinanceiras = 4
}

public sealed record DashboardExecutivoResponse(
    DateOnly DataReferencia,
    DateTime AtualizadoEmLocal,
    string FusoHorario,
    DashboardPeriodoResponse Periodo,
    DashboardResumoResponse Resumo,
    DashboardFinanceiroResponse? Financeiro,
    DashboardOperacionalResponse? Operacional,
    DashboardComercialResponse? Comercial,
    DashboardAtividadeResponse Atividade);

public sealed record DashboardPeriodoResponse(
    PeriodoDashboardContrato Periodo,
    DateOnly Inicio,
    DateOnly Fim,
    GranularidadeDashboardContrato Granularidade);

public sealed record DashboardResumoResponse(
    int? AgendamentosHoje,
    int? AgendamentosConcluidosHoje,
    int? OrdensEmExecucao,
    int? OrdensAguardandoRetirada,
    decimal? ReceitaLiquida,
    decimal? VariacaoReceitaPercentual);

public sealed record DashboardFinanceiroResponse(
    decimal RecebidoBruto,
    decimal Taxas,
    decimal ReceitaLiquida,
    decimal TicketMedio,
    int ContasPendentes,
    decimal ValorEmAberto,
    IReadOnlyCollection<DashboardReceitaPontoResponse> ReceitaAoLongoPeriodo);

public sealed record DashboardReceitaPontoResponse(DateOnly Data, decimal ReceitaLiquida);

public sealed record DashboardOperacionalResponse(
    int? ServicosRealizados,
    int? VeiculosEntregues,
    int? ClientesAtendidos,
    int? AtendimentosAtrasados,
    DashboardFluxoResponse Fluxo,
    IReadOnlyCollection<DashboardAgendamentoResponse>? AgendaHoje);

public sealed record DashboardFluxoResponse(
    int? Agenda,
    int? ClienteChegou,
    int? EmExecucao,
    int? AguardandoRetirada,
    int? Concluido);

public sealed record DashboardAgendamentoResponse(
    Guid Id,
    DateTime InicioLocal,
    string ClienteNome,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    string? ItemPrincipal,
    StatusAgendamentoContrato Status);

public sealed record DashboardComercialResponse(
    int? OrcamentosCriados,
    int? OrcamentosEnviados,
    int? OrcamentosAprovados,
    int? OrcamentosRecusados,
    int? OrcamentosAguardandoAprovacao,
    decimal? TaxaConversao,
    IReadOnlyCollection<DashboardServicoRankingResponse>? ServicosMaisRealizados);

public sealed record DashboardServicoRankingResponse(
    string Nome,
    int Quantidade,
    decimal Percentual);

public sealed record DashboardAtividadeResponse(
    IReadOnlyCollection<DashboardAtividadeItemResponse> Itens,
    IReadOnlyCollection<DashboardAtencaoItemResponse> Atencoes);

public sealed record DashboardAtividadeItemResponse(
    TipoAtividadeDashboardContrato Tipo,
    Guid EntidadeId,
    DateTime DataLocal,
    string Descricao,
    string Destino);

public sealed record DashboardAtencaoItemResponse(
    TipoAtencaoDashboardContrato Tipo,
    int Quantidade,
    decimal? Valor);
