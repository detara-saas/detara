using Detara.Application.Abstracoes;
using Detara.Domain.Agenda;
using Detara.Domain.Catalogo;

namespace Detara.Application.Agenda;

public sealed record FiltroAgendaPeriodo(DateTime InicioUtc, DateTime FimUtc, StatusAgendamento? Status, string? Pesquisa);
public sealed record FiltroHistoricoAgendamentos(int Pagina, int TamanhoPagina, DateTime? InicioUtc, DateTime? FimUtc, StatusAgendamento? Status, string? Pesquisa);
public sealed record ResumoReferenciaAgenda(decimal? SomaReferencias, bool PossuiAPartirDe, bool PossuiSobConsulta);
public sealed record AgendamentoPeriodoResultado(Guid Id, DateTime InicioUtc, int DuracaoPlanejadaMinutos, string ClienteNome, string VeiculoDescricao, string VeiculoPlaca, StatusAgendamento Status, IReadOnlyCollection<string> PrincipaisItens, ResumoReferenciaAgenda Referencia);
public sealed record AgendamentoListaResultado(Guid Id, DateTime InicioUtc, int DuracaoPlanejadaMinutos, string ClienteNome, string VeiculoDescricao, string VeiculoPlaca, StatusAgendamento Status, IReadOnlyCollection<string> Itens);
public sealed record AgendamentoItemResultado(Guid Id, TipoItemAgendamento TipoItem, Guid ItemCatalogoId, string Nome, string? Descricao, TipoPrecificacao TipoPrecificacao, decimal? PrecoReferencia, int? DuracaoReferenciaMinutos, int Ordem);
public sealed record AgendamentoDetalheResultado(Guid Id, Guid ClienteId, string ClienteNome, Guid VeiculoId, string VeiculoDescricao, string VeiculoPlaca, DateTime InicioUtc, int DuracaoPlanejadaMinutos, StatusAgendamento Status, string? ObservacaoSolicitante, string? ObservacaoInterna, string? MotivoCancelamento, DateTime CriadoEmUtc, DateTime? AtualizadoEmUtc, IReadOnlyCollection<AgendamentoItemResultado> Itens, ResumoReferenciaAgenda Referencia);

public interface IAgendaRepositorio
{
    Task<IReadOnlyCollection<AgendamentoPeriodoResultado>> ListarPeriodoAsync(FiltroAgendaPeriodo filtro, CancellationToken cancellationToken);
    Task<PaginacaoResultado<AgendamentoListaResultado>> ListarHistoricoAsync(FiltroHistoricoAgendamentos filtro, CancellationToken cancellationToken);
    Task<AgendamentoDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken cancellationToken);
    Task<Agendamento?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken);
    Task<int> ContarSobreposicoesAsync(DateTime inicioUtc, DateTime fimUtc, Guid? ignorarAgendamentoId, CancellationToken cancellationToken);
    void Adicionar(Agendamento agendamento);
    void RemoverItensAtuais(Agendamento agendamento);
    void AdicionarItensAtuais(Agendamento agendamento);
    Task SalvarAsync(CancellationToken cancellationToken);
}
