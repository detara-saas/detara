namespace Detara.Contracts.Capacidades;

public static class CodigosCapacidadeContrato
{
    public const string Veiculos = "veiculos";
    public const string CheckIn = "check-in";
}

public sealed record CapacidadeEmpresaResponse(
    string Codigo,
    string Nome,
    string Categoria,
    bool Habilitada,
    bool Configuravel,
    int Ordem);

public sealed record SnapshotCapacidadesEmpresaResponse(
    string Segmento,
    IReadOnlyCollection<CapacidadeEmpresaResponse> Capacidades);
