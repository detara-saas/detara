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
    string Placa,
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
