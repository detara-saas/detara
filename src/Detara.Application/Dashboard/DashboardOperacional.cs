using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Domain.Agenda;
using Detara.Domain.Atendimento;
using MediatR;

namespace Detara.Application.Dashboard;

public sealed record PermissoesDashboardOperacional(
    bool PodeVerAgenda,
    bool PodeVerOrdensServico,
    bool PodeVerOrcamentos,
    bool PodeVerFinanceiro);

public sealed record DashboardAgendamentoResultado(
    Guid Id,
    DateTime InicioUtc,
    string ClienteNome,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    string? ItemPrincipal,
    StatusAgendamento Status);

public sealed record DashboardAgendaResultado(
    int AgendamentosHoje,
    int ConcluidosHoje,
    IReadOnlyCollection<DashboardAgendamentoResultado> Itens);

public sealed record DashboardAtendimentoResultado(
    int OrdensEmExecucao,
    int OrdensAguardandoRetirada,
    int OrcamentosEmAberto);

public sealed record DashboardFinanceiroResultado(
    decimal RecebidoBruto,
    decimal Taxas,
    int ContasPendentes,
    decimal ValorPendente);

public sealed record DashboardOperacionalResultado(
    DateOnly DataReferencia,
    DateTime AtualizadoEmLocal,
    string FusoHorario,
    DashboardAgendaResultado? Agenda,
    DashboardAtendimentoResultado? Atendimento,
    DashboardFinanceiroResultado? Financeiro,
    DateOnly InicioPeriodoFinanceiro,
    DateOnly FimPeriodoFinanceiro);

public interface IPlataformaDashboardConsulta
{
    Task<string?> ObterFusoHorarioAsync(Guid empresaId, CancellationToken cancellationToken);
}

public interface IAgendaDashboardConsulta
{
    Task<DashboardAgendaResultado> ObterAsync(
        Guid empresaId,
        DateTime inicioUtc,
        DateTime fimExclusivoUtc,
        int limite,
        CancellationToken cancellationToken);
}

public interface IAtendimentoDashboardConsulta
{
    Task<DashboardAtendimentoResultado> ObterAsync(
        Guid empresaId,
        DateOnly hojeLocal,
        bool consultarOrdensServico,
        bool consultarOrcamentos,
        CancellationToken cancellationToken);
}

public interface IFinanceiroDashboardConsulta
{
    Task<DashboardFinanceiroResultado> ObterAsync(
        Guid empresaId,
        DateTime inicioPeriodoUtc,
        DateTime fimPeriodoExclusivoUtc,
        CancellationToken cancellationToken);
}

public sealed record ObterDashboardOperacionalQuery(PermissoesDashboardOperacional Permissoes)
    : IRequest<DashboardOperacionalResultado>;

internal sealed class ObterDashboardOperacionalHandler(
    IUsuarioContexto usuario,
    IPlataformaDashboardConsulta plataforma,
    IAgendaDashboardConsulta agenda,
    IAtendimentoDashboardConsulta atendimento,
    IFinanceiroDashboardConsulta financeiro,
    IConversorFusoHorario conversor,
    TimeProvider timeProvider)
    : IRequestHandler<ObterDashboardOperacionalQuery, DashboardOperacionalResultado>
{
    private const int LimiteAgenda = 5;

    public async Task<DashboardOperacionalResultado> Handle(
        ObterDashboardOperacionalQuery request,
        CancellationToken cancellationToken)
    {
        var empresaId = usuario.EmpresaId;
        var fusoHorario = await plataforma.ObterFusoHorarioAsync(empresaId, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada para compor o Dashboard.");
        var agoraUtc = timeProvider.GetUtcNow().UtcDateTime;
        var agoraLocal = conversor.ParaLocal(agoraUtc, fusoHorario);
        var hojeLocal = DateOnly.FromDateTime(agoraLocal);
        var inicioHojeUtc = conversor.ParaUtc(
            hojeLocal.ToDateTime(TimeOnly.MinValue),
            fusoHorario);
        var fimHojeUtc = conversor.ParaUtc(
            hojeLocal.AddDays(1).ToDateTime(TimeOnly.MinValue),
            fusoHorario);
        var inicioPeriodo = new DateOnly(hojeLocal.Year, hojeLocal.Month, 1);
        var fimPeriodo = inicioPeriodo.AddMonths(1).AddDays(-1);
        var inicioPeriodoUtc = conversor.ParaUtc(
            inicioPeriodo.ToDateTime(TimeOnly.MinValue),
            fusoHorario);
        var fimPeriodoExclusivoUtc = conversor.ParaUtc(
            fimPeriodo.AddDays(1).ToDateTime(TimeOnly.MinValue),
            fusoHorario);

        DashboardAgendaResultado? resumoAgenda = null;
        DashboardAtendimentoResultado? resumoAtendimento = null;
        DashboardFinanceiroResultado? resumoFinanceiro = null;

        if (request.Permissoes.PodeVerAgenda)
        {
            resumoAgenda = await agenda.ObterAsync(
                empresaId,
                inicioHojeUtc,
                fimHojeUtc,
                LimiteAgenda,
                cancellationToken);
        }

        if (request.Permissoes.PodeVerOrdensServico || request.Permissoes.PodeVerOrcamentos)
        {
            resumoAtendimento = await atendimento.ObterAsync(
                empresaId,
                hojeLocal,
                request.Permissoes.PodeVerOrdensServico,
                request.Permissoes.PodeVerOrcamentos,
                cancellationToken);
        }

        if (request.Permissoes.PodeVerFinanceiro)
        {
            resumoFinanceiro = await financeiro.ObterAsync(
                empresaId,
                inicioPeriodoUtc,
                fimPeriodoExclusivoUtc,
                cancellationToken);
        }

        return new(
            hojeLocal,
            agoraLocal,
            fusoHorario,
            resumoAgenda,
            resumoAtendimento,
            resumoFinanceiro,
            inicioPeriodo,
            fimPeriodo);
    }
}
