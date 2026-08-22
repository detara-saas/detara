using Detara.Contracts.Catalogo;

namespace Detara.Contracts.Atendimento;

public enum StatusOrdemServicoContrato { Aberta = 1, EmExecucao = 2, AguardandoRetirada = 3, Concluida = 4, Cancelada = 5 }
public enum OrigemOrdemServicoContrato { Orcamento = 1, Agendamento = 2, AtendimentoDireto = 3 }
public enum OrigemComercialOrdemServicoContrato { Orcamento = 1, AcordoDireto = 2, Cortesia = 3 }
public enum RespostaChecklistOrdemServicoContrato { Conforme = 1, NaoConforme = 2, NaoAplicavel = 3 }
public enum CategoriaFotoOrdemServicoContrato { Entrada = 1, Durante = 2, Saida = 3 }

public sealed record ItemOrdemServicoRequest(TipoItemOrcamentoContrato TipoItem, Guid? ItemCatalogoId,
    string? Nome, string? Descricao, decimal ValorUnitarioAutorizado, int Quantidade, string? ObservacaoAutorizacao);
public sealed record CriarOrdemServicoRequest(Guid? OrcamentoOrigemId, Guid? AgendamentoOrigemId,
    Guid? ClienteId, Guid? VeiculoId, int? DuracaoPlanejadaMinutos, decimal Desconto, decimal Acrescimo,
    string? ObservacaoAutorizacaoDireta, IReadOnlyCollection<ItemOrdemServicoRequest> Itens);
public sealed record RealizarCheckInRequest(int? QuilometragemEntrada, string? ObservacaoEntrada);
public sealed record RespostaChecklistOrdemServicoRequest(Guid ItemId, RespostaChecklistOrdemServicoContrato Resposta,
    string? Observacao);
public sealed record AtualizarChecklistOrdemServicoRequest(IReadOnlyCollection<RespostaChecklistOrdemServicoRequest> Respostas);
public sealed record TransicaoOrdemServicoRequest(string? Observacao);
public sealed record CancelarOrdemServicoRequest(string Motivo);
public sealed record CriarOrcamentoAdicionalRequest(DateOnly ValidoAte, string? ObservacaoCliente,
    string? ObservacaoInterna, string? Condicoes, decimal Desconto, decimal Acrescimo,
    IReadOnlyCollection<OrcamentoItemRequest> Itens);

public sealed record OrdemServicoListaResponse(Guid Id, string Codigo, string ClienteNome,
    string VeiculoDescricao, string? VeiculoPlaca, StatusOrdemServicoContrato Status,
    decimal TotalAutorizado, DateTime CriadoEmUtc);
public sealed record OrdemServicoAgendamentoResponse(Guid Id, string Codigo,
    StatusOrdemServicoContrato Status);
public sealed record VinculoOrdemServicoAgendamentoResponse(OrdemServicoAgendamentoResponse? OrdemServico);
public sealed record OrdemServicoItemResponse(Guid Id, TipoItemOrcamentoContrato TipoItem, Guid? ItemCatalogoId,
    Guid? OrcamentoOrigemId, Guid? OrcamentoItemOrigemId, string Nome, string? Descricao,
    decimal ValorUnitarioAutorizado, int Quantidade, decimal Subtotal, int Ordem,
    OrigemComercialOrdemServicoContrato OrigemComercial, DateTime AutorizadoEmUtc,
    Guid AutorizadoPorUsuarioId, string AutorizadoPorUsuarioNome, string? ObservacaoAutorizacao);
public sealed record OrdemServicoChecklistItemResponse(Guid Id, string Descricao, int Ordem,
    RespostaChecklistOrdemServicoContrato? Resposta, string? Observacao);
public sealed record OrdemServicoChecklistResponse(Guid Id, string Nome, bool Completo,
    IReadOnlyCollection<OrdemServicoChecklistItemResponse> Itens);
public sealed record OrdemServicoFotoResponse(Guid Id, CategoriaFotoOrdemServicoContrato Categoria,
    string NomeOriginal, string ContentType, long TamanhoBytes, Guid EnviadaPorUsuarioId, DateTime CriadoEmUtc);
public sealed record HistoricoStatusOrdemServicoResponse(Guid Id, StatusOrdemServicoContrato Status,
    DateTime DataUtc, Guid UsuarioId, string UsuarioNome, string? Observacao);
public sealed record OrcamentoAdicionalOrdemServicoResponse(Guid Id, string? Codigo,
    StatusOrcamentoContrato Status, decimal Total, DateTime CriadoEmUtc,
    IReadOnlyCollection<string> Itens);
public sealed record OrdemServicoDetalheResponse(Guid Id, string Codigo, OrigemOrdemServicoContrato Origem,
    Guid? OrcamentoOrigemId, Guid? AgendamentoOrigemId, Guid ClienteId, string ClienteNome,
    string? ClienteDocumento, string? ClienteTelefone, Guid VeiculoId, string VeiculoDescricao,
    string? VeiculoPlaca, int? DuracaoPlanejadaMinutos, StatusOrdemServicoContrato Status,
    decimal SubtotalAutorizado, decimal DescontoAutorizado, decimal AcrescimoAutorizado,
    decimal TotalAutorizado, DateTime? AutorizacaoDiretaEmUtc, Guid? AutorizacaoDiretaPorUsuarioId,
    string? ObservacaoAutorizacaoDireta, DateTime? CheckInEmUtc, int? QuilometragemEntrada,
    string? ObservacaoEntrada, NivelExigenciaOperacionalContrato? ChecklistEntradaSnapshot,
    NivelExigenciaOperacionalContrato? FotosEntradaSnapshot, NivelExigenciaOperacionalContrato? FotosSaidaSnapshot,
    DateTime? IniciadaEmUtc, DateTime? ExecucaoFinalizadaEmUtc, DateTime? ConcluidaEmUtc,
    DateTime? CanceladaEmUtc, string? MotivoCancelamento, DateTime CriadoEmUtc,
    IReadOnlyCollection<OrdemServicoItemResponse> Itens, OrdemServicoChecklistResponse? Checklist,
    IReadOnlyCollection<OrdemServicoFotoResponse> Fotos,
    IReadOnlyCollection<OrcamentoAdicionalOrdemServicoResponse> OrcamentosAdicionais,
    IReadOnlyCollection<HistoricoStatusOrdemServicoResponse> Historico);
