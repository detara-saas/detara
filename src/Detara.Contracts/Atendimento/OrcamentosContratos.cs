using Detara.Contracts.Catalogo;
using Detara.Contracts.Comum;

namespace Detara.Contracts.Atendimento;

public enum StatusOrcamentoContrato
{
    Rascunho = 1,
    Emitido = 2,
    Aprovado = 3,
    Recusado = 4,
    Cancelado = 5,
    Substituido = 6,
    Expirado = 7
}

public enum TipoItemOrcamentoContrato
{
    Servico = 1,
    Pacote = 2,
    Personalizado = 3
}

public sealed record OrcamentoItemRequest(
    TipoItemOrcamentoContrato TipoItem,
    Guid? ItemCatalogoId,
    string? Nome,
    string? Descricao,
    decimal ValorUnitario,
    int Quantidade,
    string? Observacao);

public sealed record SalvarOrcamentoRequest(
    Guid ClienteId,
    Guid VeiculoId,
    Guid? AgendamentoOrigemId,
    DateOnly ValidoAte,
    string? ObservacaoCliente,
    string? ObservacaoInterna,
    string? Condicoes,
    decimal Desconto,
    decimal Acrescimo,
    IReadOnlyCollection<OrcamentoItemRequest> Itens);

public sealed record RegistrarTransicaoOrcamentoRequest(string? Observacao);

public sealed record AgendarOrcamentoRequest(DateTime InicioLocal, int DuracaoPlanejadaMinutos,
    string? ObservacaoSolicitante, string? ObservacaoInterna);
public sealed record AgendamentoOrcamentoResponse(Guid AgendamentoId);

public sealed record OrcamentoListaResponse(
    Guid Id,
    string? Codigo,
    string ClienteNome,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    DateTime? EmitidoEmUtc,
    DateOnly ValidoAte,
    decimal Total,
    StatusOrcamentoContrato Status);

public sealed record OrcamentoItemResponse(
    Guid Id,
    TipoItemOrcamentoContrato TipoItem,
    Guid? ItemCatalogoId,
    string Nome,
    string? Descricao,
    TipoPrecificacaoCatalogo? TipoPrecificacaoReferencia,
    decimal? PrecoReferencia,
    decimal ValorUnitario,
    int Quantidade,
    decimal Subtotal,
    int Ordem,
    string? Observacao);

public sealed record HistoricoStatusOrcamentoResponse(
    Guid Id,
    StatusOrcamentoContrato Status,
    DateTime DataUtc,
    Guid UsuarioId,
    string UsuarioNome,
    string? Observacao);

public sealed record ReferenciaOrcamentoResponse(Guid Id, string? Codigo, StatusOrcamentoContrato Status);

public sealed record OrcamentoDetalheResponse(
    Guid Id,
    string? Codigo,
    Guid ClienteId,
    string ClienteNome,
    string? ClienteDocumento,
    string? ClienteTelefone,
    Guid VeiculoId,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    Guid? AgendamentoOrigemId,
    Guid? AgendamentoId,
    Guid? OrdemServicoOrigemId,
    Guid? OrdemServicoId,
    StatusOrcamentoContrato Status,
    DateOnly ValidoAte,
    string? ObservacaoCliente,
    string? ObservacaoInterna,
    string? Condicoes,
    decimal Subtotal,
    decimal Desconto,
    decimal Acrescimo,
    decimal Total,
    DateTime CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    DateTime? EmitidoEmUtc,
    DateTime? AprovadoEmUtc,
    DateTime? RecusadoEmUtc,
    DateTime? CanceladoEmUtc,
    DateTime? SubstituidoEmUtc,
    Guid? AprovadoPorUsuarioId,
    IReadOnlyCollection<OrcamentoItemResponse> Itens,
    IReadOnlyCollection<HistoricoStatusOrcamentoResponse> Historico,
    ReferenciaOrcamentoResponse? CriadoAPartirDe,
    ReferenciaOrcamentoResponse? SubstituidoPor);

public sealed record ClienteOrcamentoResponse(Guid Id, string Nome, string? Documento, string? Telefone);
public sealed record VeiculoOrcamentoResponse(Guid Id, string Descricao, string? Placa);
public sealed record ItemCatalogoOrcamentoResponse(
    TipoItemOrcamentoContrato TipoItem,
    Guid Id,
    string Nome,
    string? Descricao,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? PrecoReferencia);

public sealed record OrigemAgendamentoOrcamentoResponse(
    Guid AgendamentoId,
    Guid ClienteId,
    string ClienteNome,
    Guid VeiculoId,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    IReadOnlyCollection<ItemCatalogoOrcamentoResponse> Itens);

public sealed record ContextoOrcamentoResponse(DateOnly HojeLocal, DateOnly ValidadeSugerida);

public sealed record PaginaOrcamentosResponse(
    IReadOnlyCollection<OrcamentoListaResponse> Itens,
    int Pagina,
    int TamanhoPagina,
    int TotalItens,
    int TotalPaginas);
