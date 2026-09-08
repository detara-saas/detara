namespace Detara.Contracts.Relatorios;

public enum PeriodoRelatorioContrato { Hoje = 1, Ultimos7Dias, Ultimos30Dias, EsteMes, MesAnterior, EsteAno, Personalizado }
public enum PerspectivaRelatorioContrato { Geral = 1, Servicos, Clientes, Financeiro, Operacao }
public sealed record RankingRelatorioResponse(Guid? Id, string Nome, int Quantidade, decimal Valor);
public sealed record DiaRelatorioResponse(DateOnly Data, decimal Receita, decimal Despesa, int Atendimentos);
public sealed record FinanceiroRelatorioResponse(decimal Receita, decimal Despesas, decimal Resultado,
    int Recebimentos, int Pagamentos, decimal AReceber, decimal APagar, decimal Vencido,
    IReadOnlyList<DiaRelatorioResponse> Serie, IReadOnlyList<RankingRelatorioResponse> Categorias);
public sealed record AtendimentoRelatorioResponse(int Concluidos, decimal Valor, decimal Ticket, int Clientes, int Novos, int Recorrentes,
    int QuantidadeServicos, IReadOnlyList<RankingRelatorioResponse> ServicosQuantidade,
    IReadOnlyList<RankingRelatorioResponse> ServicosValor, IReadOnlyList<RankingRelatorioResponse> ClientesConsumo,
    IReadOnlyList<RankingRelatorioResponse> ClientesFrequencia, IReadOnlyList<DiaRelatorioResponse> Dias);
public sealed record OrcamentosRelatorioResponse(int Criados, int Aprovados, int Recusados, decimal? Conversao);
public sealed record AgendaRelatorioResponse(int Agendamentos, int Cancelados, int NaoCompareceu);
public sealed record InsightRelatorioResponse(string Tipo, string Nome, decimal Valor, decimal? Percentual);
public sealed record RelatorioResponse(DateOnly Inicio, DateOnly Fim, DateOnly InicioAnterior, DateOnly FimAnterior,
    string Fuso, FinanceiroRelatorioResponse? Financeiro, AtendimentoRelatorioResponse? Atendimento,
    OrcamentosRelatorioResponse? Orcamentos, AgendaRelatorioResponse? Agenda,
    decimal? VariacaoReceita, decimal? VariacaoTicket, IReadOnlyList<InsightRelatorioResponse> Insights);
