using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Application.Dashboard;
using MediatR;

namespace Detara.Application.Relatorios;

public sealed record RankingRelatorio(Guid? Id, string Nome, int Quantidade, decimal Valor);
public sealed record DiaRelatorio(DateOnly Data, decimal Receita, decimal Despesa, int Atendimentos = 0);
public sealed record FinanceiroRelatorio(decimal Receita, decimal Despesas, int Recebimentos, int Pagamentos,
    decimal AReceber, decimal APagar, decimal Vencido, IReadOnlyList<DiaRelatorio> Serie,
    IReadOnlyList<RankingRelatorio> Categorias)
{
    public decimal Resultado => Receita - Despesas;
}
public sealed record AtendimentoRelatorio(int Concluidos, decimal Valor, int Clientes, int Novos,
    int QuantidadeServicos, IReadOnlyList<RankingRelatorio> ServicosQuantidade,
    IReadOnlyList<RankingRelatorio> ServicosValor, IReadOnlyList<RankingRelatorio> ClientesConsumo,
    IReadOnlyList<RankingRelatorio> ClientesFrequencia, IReadOnlyList<DiaRelatorio> Dias)
{
    public decimal Ticket => Concluidos == 0 ? 0 : decimal.Round(Valor / Concluidos, 2);
    public int Recorrentes => Clientes - Novos;
}
public sealed record OrcamentosRelatorio(int Criados, int Aprovados, int Recusados)
{
    public decimal? Conversao => Aprovados + Recusados == 0 ? null : decimal.Round(Aprovados * 100m / (Aprovados + Recusados), 1);
}
public sealed record AgendaRelatorio(int Agendamentos, int Cancelados, int NaoCompareceu);
public sealed record InsightRelatorio(string Tipo, string Nome, decimal Valor, decimal? Percentual = null);
public sealed record PermissoesRelatorios(bool Financeiro, bool Ordens, bool Orcamentos, bool Clientes, bool Agenda);
public sealed record RelatorioResultado(PeriodoRelatorioResolvido Periodo, FinanceiroRelatorio? Financeiro,
    AtendimentoRelatorio? Atendimento, OrcamentosRelatorio? Orcamentos, AgendaRelatorio? Agenda,
    decimal? VariacaoReceita, decimal? VariacaoTicket, IReadOnlyList<InsightRelatorio> Insights);

// Contratos de leitura: cada implementação pertence ao módulo dono dos dados.
public interface IFinanceiroRelatoriosConsulta
{
    Task<FinanceiroRelatorio> ObterAsync(Guid empresaId, IntervaloRelatorio periodo, DateOnly hoje, string fuso, bool detalhar, CancellationToken ct);
}
public interface IAtendimentoRelatoriosConsulta
{
    Task<AtendimentoRelatorio> ObterAsync(Guid empresaId, IntervaloRelatorio periodo, string fuso, bool servicos, bool clientes, bool dias, CancellationToken ct);
    Task<OrcamentosRelatorio> OrcamentosAsync(Guid empresaId, IntervaloRelatorio periodo, CancellationToken ct);
}
public interface IAgendaRelatoriosConsulta
{
    Task<AgendaRelatorio> ObterAsync(Guid empresaId, IntervaloRelatorio periodo, CancellationToken ct);
}

public sealed record ObterRelatorioQuery(PerspectivaRelatorio Perspectiva, PeriodoRelatorio Periodo,
    DateOnly? Inicio, DateOnly? Fim, PermissoesRelatorios Permissoes) : IRequest<RelatorioResultado>;

internal sealed class ObterRelatorioHandler(IUsuarioContexto usuario, IPlataformaDashboardConsulta plataforma,
    IConversorFusoHorario conversor, TimeProvider relogio, IFinanceiroRelatoriosConsulta financeiro,
    IAtendimentoRelatoriosConsulta atendimento, IAgendaRelatoriosConsulta agenda)
    : IRequestHandler<ObterRelatorioQuery, RelatorioResultado>
{
    public async Task<RelatorioResultado> Handle(ObterRelatorioQuery request, CancellationToken ct)
    {
        var fuso = await plataforma.ObterFusoHorarioAsync(usuario.EmpresaId, ct)
            ?? throw new RecursoNaoEncontradoException("Empresa não encontrada.");
        var hoje = DateOnly.FromDateTime(conversor.ParaLocal(relogio.GetUtcNow().UtcDateTime, fuso));
        var p = PeriodosRelatorio.Resolver(request.Periodo, request.Inicio, request.Fim, hoje, fuso, conversor);
        var geral = request.Perspectiva == PerspectivaRelatorio.Geral;
        FinanceiroRelatorio? f = null, fa = null;
        AtendimentoRelatorio? a = null, aa = null;
        OrcamentosRelatorio? o = null;
        AgendaRelatorio? g = null;
        if (request.Permissoes.Financeiro && (geral || request.Perspectiva == PerspectivaRelatorio.Financeiro))
        {
            f = await financeiro.ObterAsync(usuario.EmpresaId, p.Atual, hoje, fuso, true, ct);
            fa = await financeiro.ObterAsync(usuario.EmpresaId, p.Anterior, hoje, fuso, false, ct);
        }
        if (request.Permissoes.Ordens && request.Perspectiva != PerspectivaRelatorio.Financeiro)
        {
            a = await atendimento.ObterAsync(usuario.EmpresaId, p.Atual, fuso,
                geral || request.Perspectiva == PerspectivaRelatorio.Servicos,
                request.Permissoes.Clientes && request.Perspectiva == PerspectivaRelatorio.Clientes,
                geral || request.Perspectiva == PerspectivaRelatorio.Operacao, ct);
            aa = await atendimento.ObterAsync(usuario.EmpresaId, p.Anterior, fuso, false, false, false, ct);
        }
        if (request.Permissoes.Orcamentos && request.Perspectiva == PerspectivaRelatorio.Servicos)
            o = await atendimento.OrcamentosAsync(usuario.EmpresaId, p.Atual, ct);
        if (request.Permissoes.Agenda && request.Perspectiva == PerspectivaRelatorio.Operacao)
            g = await agenda.ObterAsync(usuario.EmpresaId, p.Atual, ct);
        return new(p, f, a, o, g,
            f is null ? null : PeriodosRelatorio.Variacao(f.Receita, fa!.Receita),
            a is null ? null : PeriodosRelatorio.Variacao(a.Ticket, aa!.Ticket),
            geral ? InsightsRelatorio.Criar(f, fa, a, aa) : []);
    }
}
