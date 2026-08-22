namespace Detara.Contracts.Veiculos;

public enum TipoVeiculoContrato
{
    Carro = 1,
    Moto = 2,
    Caminhonete = 3,
    Van = 4,
    Caminhao = 5,
    Embarcacao = 6,
    MotoAquatica = 7,
    QuadricicloUtv = 8,
    Outro = 99
}

public sealed record SalvarVeiculoRequest(
    Guid ClienteId,
    TipoVeiculoContrato Tipo,
    string? Placa,
    string? IdentificacaoAlternativa,
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
    TipoVeiculoContrato Tipo,
    string? Placa,
    string? IdentificacaoAlternativa,
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
    TipoVeiculoContrato Tipo,
    string? Placa,
    string? IdentificacaoAlternativa,
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

public sealed record VeiculoFotoResponse(
    Guid Id,
    Guid VeiculoId,
    string NomeOriginal,
    string ContentType,
    long TamanhoBytes,
    bool EhPrincipal,
    DateTime CriadoEmUtc);
