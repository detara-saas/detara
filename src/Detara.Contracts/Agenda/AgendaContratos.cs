using Detara.Contracts.Catalogo;
using Detara.Contracts.Comum;

namespace Detara.Contracts.Agenda;

public enum StatusAgendamentoContrato
{
    Agendado = 1,
    Confirmado = 2,
    Compareceu = 3,
    Concluido = 4,
    Cancelado = 5,
    NaoCompareceu = 6
}

public enum TipoItemAgendamentoContrato
{
    Servico = 1,
    Pacote = 2
}

public sealed record AgendamentoItemRequest(TipoItemAgendamentoContrato TipoItem, Guid ItemCatalogoId);

public sealed record SalvarAgendamentoRequest(
    Guid ClienteId,
    Guid VeiculoId,
    DateTime InicioLocal,
    int DuracaoPlanejadaMinutos,
    string? ObservacaoSolicitante,
    string? ObservacaoInterna,
    IReadOnlyCollection<AgendamentoItemRequest> Itens);

public sealed record ReagendarAgendamentoRequest(DateTime InicioLocal, int DuracaoPlanejadaMinutos);
public sealed record AlterarStatusAgendamentoRequest(StatusAgendamentoContrato Status, string? MotivoCancelamento);

public sealed record AgendamentoItemResponse(
    Guid Id,
    TipoItemAgendamentoContrato TipoItem,
    Guid ItemCatalogoId,
    string Nome,
    string? Descricao,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? PrecoReferencia,
    int? DuracaoReferenciaMinutos,
    int Ordem,
    bool ItemAtivoNoCatalogo);

public sealed record ResumoReferenciaAgendamentoResponse(
    decimal? SomaReferencias,
    bool PossuiAPartirDe,
    bool PossuiSobConsulta,
    string Texto);

public sealed record AgendamentoPeriodoResponse(
    Guid Id,
    DateTime InicioUtc,
    DateTime InicioLocal,
    int DuracaoPlanejadaMinutos,
    string ClienteNome,
    string VeiculoDescricao,
    string VeiculoPlaca,
    StatusAgendamentoContrato Status,
    IReadOnlyCollection<string> PrincipaisItens,
    ResumoReferenciaAgendamentoResponse Referencia);

public sealed record AgendamentoListaResponse(
    Guid Id,
    DateTime InicioUtc,
    DateTime InicioLocal,
    int DuracaoPlanejadaMinutos,
    string ClienteNome,
    string VeiculoDescricao,
    string VeiculoPlaca,
    StatusAgendamentoContrato Status,
    IReadOnlyCollection<string> Itens);

public sealed record AgendamentoDetalheResponse(
    Guid Id,
    Guid ClienteId,
    string ClienteNome,
    Guid VeiculoId,
    string VeiculoDescricao,
    string VeiculoPlaca,
    DateTime InicioUtc,
    DateTime InicioLocal,
    string FusoHorario,
    int DuracaoPlanejadaMinutos,
    StatusAgendamentoContrato Status,
    string? ObservacaoSolicitante,
    string? ObservacaoInterna,
    string? MotivoCancelamento,
    DateTime CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    int QuantidadeSobreposicoes,
    ResumoReferenciaAgendamentoResponse Referencia,
    IReadOnlyCollection<AgendamentoItemResponse> Itens);

public sealed record ClienteAgendaResponse(Guid Id, string Nome, string? Telefone);
public sealed record VeiculoAgendaResponse(Guid Id, string Descricao, string Placa);
public sealed record ContextoAgendaResponse(string FusoHorario, DateOnly HojeLocal, DateTime AgoraLocal);
public sealed record ItemCatalogoAgendaResponse(
    TipoItemAgendamentoContrato TipoItem,
    Guid Id,
    string Nome,
    string? Descricao,
    string? Categoria,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? PrecoReferencia,
    int? DuracaoReferenciaMinutos,
    bool EhAtivo);

public sealed record PaginaAgendamentosResponse(
    IReadOnlyCollection<AgendamentoListaResponse> Itens,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);
