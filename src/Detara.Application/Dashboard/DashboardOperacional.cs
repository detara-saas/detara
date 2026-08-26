using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Domain.Agenda;
using MediatR;

namespace Detara.Application.Dashboard;

public enum PeriodoDashboard
{
    Hoje = 1,
    Ultimos7Dias = 2,
    EsteMes = 3,
    EsteAno = 4
}

public enum GranularidadeDashboard
{
    Dia = 1,
    Mes = 2
}

public enum TipoAtividadeDashboard
{
    AgendamentoCriado = 1,
    OrcamentoAprovado = 2,
    ClienteChegou = 3,
    OrdemServicoIniciada = 4,
    VeiculoEntregue = 5,
    PagamentoRecebido = 6,
    ComunicacaoEnviada = 7
}

public enum TipoAtencaoDashboard
{
    VeiculosAguardandoRetirada = 1,
    OrcamentosAguardandoAprovacao = 2,
    AtendimentosAtrasados = 3,
    PendenciasFinanceiras = 4
}

public sealed record PermissoesDashboardOperacional(
    bool PodeVerAgenda,
    bool PodeVerOrdensServico,
    bool PodeVerOrcamentos,
    bool PodeVerFinanceiro);

public sealed record DashboardPeriodoDto(
    PeriodoDashboard Periodo,
    DateOnly Inicio,
    DateOnly Fim,
    DateTime InicioUtc,
    DateTime FimExclusivoUtc,
    DateTime InicioAnteriorUtc,
    DateTime FimAnteriorExclusivoUtc,
    GranularidadeDashboard Granularidade);

public sealed record DashboardAgendamentoDto(
    Guid Id,
    DateTime InicioUtc,
    string ClienteNome,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    string? ItemPrincipal,
    StatusAgendamento Status);

public sealed record DashboardReceitaPontoDto(DateOnly Data, decimal ReceitaLiquida);

public sealed record DashboardServicoRankingDto(string Nome, int Quantidade, decimal Percentual);

public sealed record DashboardFluxoDto(
    int? Agenda,
    int? ClienteChegou,
    int? EmExecucao,
    int? AguardandoRetirada,
    int? Concluido);

public sealed record DashboardAtividadeItemDto(
    TipoAtividadeDashboard Tipo,
    Guid EntidadeId,
    DateTime DataUtc,
    string Descricao);

public sealed record DashboardAtencaoItemDto(
    TipoAtencaoDashboard Tipo,
    int Quantidade,
    decimal? Valor = null);

public sealed record DashboardResumoDto(
    int? AgendamentosHoje,
    int? AgendamentosConcluidosHoje,
    int? OrdensEmExecucao,
    int? OrdensAguardandoRetirada,
    decimal? ReceitaLiquida,
    decimal? VariacaoReceitaPercentual);

public sealed record DashboardFinanceiroDto(
    decimal RecebidoBruto,
    decimal Taxas,
    decimal ReceitaLiquida,
    decimal TicketMedio,
    int ContasPendentes,
    decimal ValorEmAberto,
    IReadOnlyCollection<DashboardReceitaPontoDto> ReceitaAoLongoPeriodo);

public sealed record DashboardOperacionalDto(
    int? ServicosRealizados,
    int? VeiculosEntregues,
    int? ClientesAtendidos,
    int? AtendimentosAtrasados,
    DashboardFluxoDto Fluxo,
    IReadOnlyCollection<DashboardAgendamentoDto>? AgendaHoje);

public sealed record DashboardComercialDto(
    int? OrcamentosCriados,
    int? OrcamentosEnviados,
    int? OrcamentosAprovados,
    int? OrcamentosRecusados,
    int? OrcamentosAguardandoAprovacao,
    decimal? TaxaConversao,
    IReadOnlyCollection<DashboardServicoRankingDto>? ServicosMaisRealizados);

public sealed record DashboardAtividadeDto(
    IReadOnlyCollection<DashboardAtividadeItemDto> Itens,
    IReadOnlyCollection<DashboardAtencaoItemDto> Atencoes);

public sealed record DashboardExecutivoResultado(
    DateOnly DataReferencia,
    DateTime AtualizadoEmLocal,
    string FusoHorario,
    DashboardPeriodoDto Periodo,
    DashboardResumoDto Resumo,
    DashboardFinanceiroDto? Financeiro,
    DashboardOperacionalDto? Operacional,
    DashboardComercialDto? Comercial,
    DashboardAtividadeDto Atividade);

public sealed record DashboardAgendaConsultaResultado(
    int AgendamentosHoje,
    int ConcluidosHoje,
    int AgendamentosPeriodo,
    int AtendimentosAtrasados,
    IReadOnlyCollection<DashboardAgendamentoDto> ItensHoje,
    IReadOnlyCollection<DashboardAtividadeItemDto> Atividades);

public sealed record DashboardAtendimentoConsultaResultado(
    int OrdensEmExecucao,
    int OrdensAguardandoRetirada,
    int OrcamentosAguardandoAprovacao,
    int ServicosRealizados,
    int VeiculosEntregues,
    int ClientesAtendidos,
    int OrcamentosCriados,
    int OrcamentosEnviados,
    int OrcamentosAprovados,
    int OrcamentosRecusados,
    int ClientesQueChegaram,
    int OrdensEmExecucaoPeriodo,
    int OrdensAguardandoPeriodo,
    int OrdensConcluidasPeriodo,
    IReadOnlyCollection<DashboardServicoQuantidadeConsulta> ServicosMaisRealizados,
    IReadOnlyCollection<DashboardAtividadeItemDto> Atividades);

public sealed record DashboardServicoQuantidadeConsulta(string Nome, int Quantidade);

public sealed record DashboardReceitaPontoConsulta(DateOnly Data, decimal ReceitaLiquida);

public sealed record DashboardFinanceiroConsultaResultado(
    decimal RecebidoBruto,
    decimal Taxas,
    decimal ReceitaLiquidaAnterior,
    decimal TicketMedio,
    int ContasPendentes,
    decimal ValorEmAberto,
    IReadOnlyCollection<DashboardReceitaPontoConsulta> Receita,
    IReadOnlyCollection<DashboardAtividadeItemDto> Atividades);

public interface IPlataformaDashboardConsulta
{
    Task<string?> ObterFusoHorarioAsync(Guid empresaId, CancellationToken cancellationToken);
}

public interface IAgendaDashboardConsulta
{
    Task<DashboardAgendaConsultaResultado> ObterAsync(
        Guid empresaId,
        DashboardPeriodoDto periodo,
        DateTime inicioHojeUtc,
        DateTime fimHojeExclusivoUtc,
        DateTime agoraUtc,
        int limiteAgenda,
        int limiteAtividades,
        CancellationToken cancellationToken);
}

public interface IAtendimentoDashboardConsulta
{
    Task<DashboardAtendimentoConsultaResultado> ObterAsync(
        Guid empresaId,
        DashboardPeriodoDto periodo,
        DateOnly hojeLocal,
        bool consultarOrdensServico,
        bool consultarOrcamentos,
        int limiteRanking,
        int limiteAtividades,
        CancellationToken cancellationToken);
}

public interface IFinanceiroDashboardConsulta
{
    Task<DashboardFinanceiroConsultaResultado> ObterAsync(
        Guid empresaId,
        DashboardPeriodoDto periodo,
        string fusoHorario,
        int limiteAtividades,
        CancellationToken cancellationToken);
}

public interface INotificacoesDashboardConsulta
{
    Task<IReadOnlyCollection<DashboardAtividadeItemDto>> ObterAtividadesAsync(
        Guid empresaId,
        DashboardPeriodoDto periodo,
        int limite,
        CancellationToken cancellationToken);
}

public sealed record ObterDashboardOperacionalQuery(
    PeriodoDashboard Periodo,
    PermissoesDashboardOperacional Permissoes) : IRequest<DashboardExecutivoResultado>;

internal sealed class ObterDashboardOperacionalHandler(
    IUsuarioContexto usuario,
    IPlataformaDashboardConsulta plataforma,
    IAgendaDashboardConsulta agenda,
    IAtendimentoDashboardConsulta atendimento,
    IFinanceiroDashboardConsulta financeiro,
    INotificacoesDashboardConsulta notificacoes,
    IConversorFusoHorario conversor,
    TimeProvider timeProvider)
    : IRequestHandler<ObterDashboardOperacionalQuery, DashboardExecutivoResultado>
{
    private const int LimiteAgenda = 5;
    private const int LimiteRanking = 5;
    private const int LimiteAtividadesPorModulo = 10;
    private const int LimiteAtividades = 10;

    public async Task<DashboardExecutivoResultado> Handle(
        ObterDashboardOperacionalQuery request,
        CancellationToken cancellationToken)
    {
        if (!Enum.IsDefined(request.Periodo))
            throw new ConflitoRegraNegocioException("O período informado para o Dashboard é inválido.");

        var empresaId = usuario.EmpresaId;
        var fusoHorario = await plataforma.ObterFusoHorarioAsync(empresaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada para compor o Dashboard.");
        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        var agoraLocal = conversor.ParaLocal(agoraUtc, fusoHorario);
        var hojeLocal = DateOnly.FromDateTime(agoraLocal);
        var periodo = CriarPeriodo(request.Periodo, hojeLocal, fusoHorario);
        var inicioHojeUtc = conversor.ParaUtc(hojeLocal.ToDateTime(TimeOnly.MinValue), fusoHorario);
        var fimHojeUtc = conversor.ParaUtc(hojeLocal.AddDays(1).ToDateTime(TimeOnly.MinValue), fusoHorario);

        DashboardAgendaConsultaResultado? resumoAgenda = null;
        DashboardAtendimentoConsultaResultado? resumoAtendimento = null;
        DashboardFinanceiroConsultaResultado? resumoFinanceiro = null;
        IReadOnlyCollection<DashboardAtividadeItemDto> atividadesNotificacoes = [];

        if (request.Permissoes.PodeVerAgenda)
        {
            resumoAgenda = await agenda.ObterAsync(
                empresaId, periodo, inicioHojeUtc, fimHojeUtc, agoraUtc,
                LimiteAgenda, LimiteAtividadesPorModulo, cancellationToken);
        }

        if (request.Permissoes.PodeVerOrdensServico || request.Permissoes.PodeVerOrcamentos)
        {
            resumoAtendimento = await atendimento.ObterAsync(
                empresaId, periodo, hojeLocal,
                request.Permissoes.PodeVerOrdensServico,
                request.Permissoes.PodeVerOrcamentos,
                LimiteRanking, LimiteAtividadesPorModulo, cancellationToken);
        }

        if (request.Permissoes.PodeVerFinanceiro)
        {
            resumoFinanceiro = await financeiro.ObterAsync(
                empresaId, periodo, fusoHorario,
                LimiteAtividadesPorModulo, cancellationToken);
        }

        if (request.Permissoes.PodeVerOrdensServico)
        {
            atividadesNotificacoes = await notificacoes.ObterAtividadesAsync(
                empresaId, periodo, LimiteAtividadesPorModulo, cancellationToken);
        }

        decimal? variacaoReceita = resumoFinanceiro is { ReceitaLiquidaAnterior: > 0 }
            ? decimal.Round(
                (resumoFinanceiro.RecebidoBruto - resumoFinanceiro.Taxas - resumoFinanceiro.ReceitaLiquidaAnterior) /
                resumoFinanceiro.ReceitaLiquidaAnterior * 100,
                1)
            : null;
        var resumo = new DashboardResumoDto(
            resumoAgenda?.AgendamentosHoje,
            resumoAgenda?.ConcluidosHoje,
            request.Permissoes.PodeVerOrdensServico ? resumoAtendimento?.OrdensEmExecucao : null,
            request.Permissoes.PodeVerOrdensServico ? resumoAtendimento?.OrdensAguardandoRetirada : null,
            resumoFinanceiro is null ? null : resumoFinanceiro.RecebidoBruto - resumoFinanceiro.Taxas,
            variacaoReceita);

        var financeiroDto = resumoFinanceiro is null
            ? null
            : new DashboardFinanceiroDto(
                resumoFinanceiro.RecebidoBruto,
                resumoFinanceiro.Taxas,
                resumoFinanceiro.RecebidoBruto - resumoFinanceiro.Taxas,
                resumoFinanceiro.TicketMedio,
                resumoFinanceiro.ContasPendentes,
                resumoFinanceiro.ValorEmAberto,
                CriarSerieReceita(periodo, resumoFinanceiro.Receita));

        DashboardOperacionalDto? operacionalDto = null;
        if (resumoAgenda is not null || request.Permissoes.PodeVerOrdensServico)
        {
            operacionalDto = new(
                request.Permissoes.PodeVerOrdensServico ? resumoAtendimento?.ServicosRealizados : null,
                request.Permissoes.PodeVerOrdensServico ? resumoAtendimento?.VeiculosEntregues : null,
                request.Permissoes.PodeVerOrdensServico ? resumoAtendimento?.ClientesAtendidos : null,
                resumoAgenda?.AtendimentosAtrasados,
                new(
                    resumoAgenda?.AgendamentosPeriodo,
                    request.Permissoes.PodeVerOrdensServico ? resumoAtendimento?.ClientesQueChegaram : null,
                    request.Permissoes.PodeVerOrdensServico ? resumoAtendimento?.OrdensEmExecucaoPeriodo : null,
                    request.Permissoes.PodeVerOrdensServico ? resumoAtendimento?.OrdensAguardandoPeriodo : null,
                    request.Permissoes.PodeVerOrdensServico ? resumoAtendimento?.OrdensConcluidasPeriodo : null),
                resumoAgenda?.ItensHoje);
        }

        DashboardComercialDto? comercialDto = null;
        if (request.Permissoes.PodeVerOrcamentos || request.Permissoes.PodeVerOrdensServico)
        {
            var totalServicos = resumoAtendimento?.ServicosMaisRealizados.Sum(item => item.Quantidade) ?? 0;
            var ranking = request.Permissoes.PodeVerOrdensServico
                ? resumoAtendimento?.ServicosMaisRealizados.Select(item => new DashboardServicoRankingDto(
                    item.Nome,
                    item.Quantidade,
                    totalServicos == 0 ? 0 : decimal.Round(item.Quantidade * 100m / totalServicos, 1))).ToArray() ?? []
                : null;
            var criados = request.Permissoes.PodeVerOrcamentos ? resumoAtendimento?.OrcamentosCriados : null;
            comercialDto = new(
                criados,
                request.Permissoes.PodeVerOrcamentos ? resumoAtendimento?.OrcamentosEnviados : null,
                request.Permissoes.PodeVerOrcamentos ? resumoAtendimento?.OrcamentosAprovados : null,
                request.Permissoes.PodeVerOrcamentos ? resumoAtendimento?.OrcamentosRecusados : null,
                request.Permissoes.PodeVerOrcamentos ? resumoAtendimento?.OrcamentosAguardandoAprovacao : null,
                criados > 0 ? decimal.Round((resumoAtendimento?.OrcamentosAprovados ?? 0) * 100m / criados.Value, 1) : null,
                ranking);
        }

        var atividades = (resumoAgenda?.Atividades ?? [])
            .Concat(resumoAtendimento?.Atividades ?? [])
            .Concat(resumoFinanceiro?.Atividades ?? [])
            .Concat(atividadesNotificacoes)
            .OrderByDescending(item => item.DataUtc)
            .Take(LimiteAtividades)
            .ToArray();
        var atencoes = CriarAtencoes(resumoAgenda, resumoAtendimento, resumoFinanceiro, request.Permissoes);

        return new(
            hojeLocal,
            agoraLocal,
            fusoHorario,
            periodo,
            resumo,
            financeiroDto,
            operacionalDto,
            comercialDto,
            new(atividades, atencoes));
    }

    private DashboardPeriodoDto CriarPeriodo(
        PeriodoDashboard periodo,
        DateOnly hoje,
        string fusoHorario)
    {
        var inicio = periodo switch
        {
            PeriodoDashboard.Hoje => hoje,
            PeriodoDashboard.Ultimos7Dias => hoje.AddDays(-6),
            PeriodoDashboard.EsteMes => new DateOnly(hoje.Year, hoje.Month, 1),
            PeriodoDashboard.EsteAno => new DateOnly(hoje.Year, 1, 1),
            _ => throw new ConflitoRegraNegocioException("O período informado para o Dashboard é inválido.")
        };
        var quantidadeDias = hoje.DayNumber - inicio.DayNumber + 1;
        var inicioAnterior = inicio.AddDays(-quantidadeDias);
        var inicioUtc = conversor.ParaUtc(inicio.ToDateTime(TimeOnly.MinValue), fusoHorario);
        var fimExclusivoUtc = conversor.ParaUtc(hoje.AddDays(1).ToDateTime(TimeOnly.MinValue), fusoHorario);
        var inicioAnteriorUtc = conversor.ParaUtc(inicioAnterior.ToDateTime(TimeOnly.MinValue), fusoHorario);
        return new(
            periodo,
            inicio,
            hoje,
            inicioUtc,
            fimExclusivoUtc,
            inicioAnteriorUtc,
            inicioUtc,
            periodo == PeriodoDashboard.EsteAno ? GranularidadeDashboard.Mes : GranularidadeDashboard.Dia);
    }

    private static IReadOnlyCollection<DashboardReceitaPontoDto> CriarSerieReceita(
        DashboardPeriodoDto periodo,
        IReadOnlyCollection<DashboardReceitaPontoConsulta> pontos)
    {
        var agrupados = pontos
            .GroupBy(item => periodo.Granularidade == GranularidadeDashboard.Mes
                ? new DateOnly(item.Data.Year, item.Data.Month, 1)
                : item.Data)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(item => item.ReceitaLiquida));
        var serie = new List<DashboardReceitaPontoDto>();
        var cursor = periodo.Granularidade == GranularidadeDashboard.Mes
            ? new DateOnly(periodo.Inicio.Year, periodo.Inicio.Month, 1)
            : periodo.Inicio;
        while (cursor <= periodo.Fim)
        {
            serie.Add(new(cursor, agrupados.GetValueOrDefault(cursor)));
            cursor = periodo.Granularidade == GranularidadeDashboard.Mes
                ? cursor.AddMonths(1)
                : cursor.AddDays(1);
        }
        return serie;
    }

    private static IReadOnlyCollection<DashboardAtencaoItemDto> CriarAtencoes(
        DashboardAgendaConsultaResultado? agenda,
        DashboardAtendimentoConsultaResultado? atendimento,
        DashboardFinanceiroConsultaResultado? financeiro,
        PermissoesDashboardOperacional permissoes)
    {
        var itens = new List<DashboardAtencaoItemDto>();
        if (permissoes.PodeVerOrdensServico && atendimento?.OrdensAguardandoRetirada > 0)
            itens.Add(new(TipoAtencaoDashboard.VeiculosAguardandoRetirada, atendimento.OrdensAguardandoRetirada));
        if (permissoes.PodeVerOrcamentos && atendimento?.OrcamentosAguardandoAprovacao > 0)
            itens.Add(new(TipoAtencaoDashboard.OrcamentosAguardandoAprovacao, atendimento.OrcamentosAguardandoAprovacao));
        if (agenda?.AtendimentosAtrasados > 0)
            itens.Add(new(TipoAtencaoDashboard.AtendimentosAtrasados, agenda.AtendimentosAtrasados));
        if (financeiro?.ContasPendentes > 0)
            itens.Add(new(TipoAtencaoDashboard.PendenciasFinanceiras, financeiro.ContasPendentes, financeiro.ValorEmAberto));
        return itens;
    }
}
