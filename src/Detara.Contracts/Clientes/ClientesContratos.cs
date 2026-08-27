using Detara.Contracts.Veiculos;
using Detara.Contracts.Atendimento;
using Detara.Contracts.Notificacoes;

namespace Detara.Contracts.Clientes;

public sealed record SalvarClienteRequest(
    string Nome,
    string TipoPessoa,
    string? CpfCnpj,
    string? Telefone,
    string? WhatsApp,
    string? Email,
    DateOnly? DataNascimento,
    string? Observacao);

public sealed record ClienteListaResponse(
    Guid Id,
    string Nome,
    string TipoPessoa,
    string? CpfCnpj,
    string? Telefone,
    int QuantidadeVeiculos,
    bool EhAtivo);

public sealed record VeiculoResumoClienteResponse(
    Guid Id,
    string Descricao,
    TipoVeiculoContrato Tipo,
    string? Placa,
    string? IdentificacaoAlternativa,
    int? AnoModelo,
    string? Cor,
    int? Quilometragem,
    bool EhAtivo);

public sealed record ClienteDetalheResponse(
    Guid Id,
    string Nome,
    string TipoPessoa,
    string? CpfCnpj,
    string? Telefone,
    string? WhatsApp,
    string? Email,
    DateOnly? DataNascimento,
    string? Observacao,
    DateTime CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    bool EhAtivo,
    IReadOnlyCollection<VeiculoResumoClienteResponse> Veiculos);

public sealed record ClienteBuscaResponse(
    Guid Id,
    string Nome,
    string? Telefone,
    string? CpfCnpj);

public sealed record ResumoRelacionamentoClienteResponse(
    int QuantidadeAtendimentos,
    decimal TotalInvestido,
    decimal? TicketMedio,
    DateTime? UltimaVisitaEmUtc,
    string? ServicoMaisRealizado,
    int? FrequenciaRetornoDias);

public sealed record VeiculoRelacionamentoClienteResponse(
    VeiculoResumoClienteResponse Veiculo,
    int QuantidadeAtendimentos,
    int QuantidadeServicos,
    string? UltimoServico,
    DateTime? UltimaVisitaEmUtc);

public sealed record AtendimentoRelacionamentoClienteResponse(
    Guid Id,
    string Codigo,
    Guid VeiculoId,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    StatusOrdemServicoContrato Status,
    decimal TotalAutorizado,
    DateTime DataEmUtc,
    IReadOnlyCollection<string> Servicos);

public sealed record OrcamentoRelacionamentoClienteResponse(
    Guid Id,
    string? Codigo,
    Guid VeiculoId,
    string VeiculoDescricao,
    string? VeiculoPlaca,
    StatusOrcamentoContrato Status,
    decimal Total,
    DateTime DataEmUtc,
    IReadOnlyCollection<string> Itens);

public sealed record ComunicacaoRelacionamentoClienteResponse(
    Guid Id,
    Guid? OrdemServicoId,
    CanalComunicacaoClienteContrato Canal,
    TipoComunicacaoClienteContrato Tipo,
    StatusComunicacaoClienteContrato Status,
    OrigemComunicacaoClienteContrato Origem,
    DateTime DataEmUtc);

public sealed record ClienteRelacionamentoResponse(
    ClienteDetalheResponse Cliente,
    ResumoRelacionamentoClienteResponse? Resumo,
    IReadOnlyCollection<VeiculoRelacionamentoClienteResponse> Veiculos,
    IReadOnlyCollection<AtendimentoRelacionamentoClienteResponse> Atendimentos,
    IReadOnlyCollection<OrcamentoRelacionamentoClienteResponse> Orcamentos,
    ComunicacaoRelacionamentoClienteResponse? UltimaComunicacao,
    bool PodeVisualizarAtendimentos,
    bool PodeVisualizarOrcamentos,
    bool PodeVisualizarComunicacoes);
