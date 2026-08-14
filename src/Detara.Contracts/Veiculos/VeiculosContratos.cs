namespace Detara.Contracts.Veiculos;

public sealed record SalvarVeiculoRequest(
    Guid ClienteId,
    string Placa,
    string Marca,
    string Modelo,
    string? Versao,
    int? AnoFabricacao,
    int? AnoModelo,
    string? Cor,
    int? Quilometragem,
    string? Observacao);

public sealed record VeiculoListaResponse(
    Guid Id,
    string Descricao,
    string Placa,
    Guid ClienteId,
    string ClienteNome,
    int? AnoModelo,
    string? Cor,
    int? Quilometragem,
    bool EhAtivo);

public sealed record VeiculoDetalheResponse(
    Guid Id,
    Guid ClienteId,
    string ClienteNome,
    string Placa,
    string Marca,
    string Modelo,
    string? Versao,
    int? AnoFabricacao,
    int? AnoModelo,
    string? Cor,
    int? Quilometragem,
    string? Observacao,
    DateTime CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    bool EhAtivo);
