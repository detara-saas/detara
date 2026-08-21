namespace Detara.Contracts.Onboarding;

public sealed record OnboardingEmpresaResponse(
    bool Concluido,
    int QuantidadeConcluida,
    int QuantidadeTotal,
    IReadOnlyCollection<OnboardingEtapaResponse> Etapas);

public sealed record OnboardingEtapaResponse(
    string Codigo,
    string Titulo,
    string Descricao,
    bool Concluida,
    bool PodeExecutar,
    string? Destino);
