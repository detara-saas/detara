namespace Detara.Contracts.Atendimento;

public enum NivelExigenciaOperacionalContrato
{
    Desabilitado = 0,
    Opcional = 1,
    Obrigatorio = 2
}

public sealed record AtualizarConfiguracaoOperacionalRequest(
    NivelExigenciaOperacionalContrato ChecklistEntrada,
    NivelExigenciaOperacionalContrato FotosEntrada,
    NivelExigenciaOperacionalContrato FotosSaida);

public sealed record ChecklistModeloItemRequest(string Descricao);

public sealed record AtualizarChecklistModeloRequest(
    string Nome,
    string? Descricao,
    IReadOnlyCollection<ChecklistModeloItemRequest> Itens);

public sealed record ChecklistModeloItemResponse(
    Guid Id,
    string Descricao,
    int Ordem);

public sealed record ChecklistModeloResponse(
    Guid? Id,
    string Nome,
    string? Descricao,
    IReadOnlyCollection<ChecklistModeloItemResponse> Itens,
    DateTime? CriadoEmUtc,
    DateTime? AtualizadoEmUtc);

public sealed record ConfiguracaoOperacionalResponse(
    Guid? Id,
    NivelExigenciaOperacionalContrato ChecklistEntrada,
    NivelExigenciaOperacionalContrato FotosEntrada,
    NivelExigenciaOperacionalContrato FotosSaida,
    DateTime? CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    ChecklistModeloResponse Checklist);
