namespace Detara.Contracts.Catalogo;

public enum TipoPrecificacaoCatalogo
{
    Fixo = 1,
    APartirDe = 2,
    SobConsulta = 3
}

public sealed record SalvarCategoriaServicoRequest(string Nome, string? Descricao, int Ordem);
public sealed record CategoriaServicoResponse(Guid Id, string Nome, string? Descricao, int Ordem, int QuantidadeServicos, bool EhAtivo);

public sealed record SalvarServicoRequest(
    Guid CategoriaServicoId,
    string Nome,
    string? Descricao,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? PrecoBase,
    int? DuracaoEstimadaMinutos,
    int Ordem);

public sealed record ServicoListaResponse(
    Guid Id,
    string Nome,
    Guid CategoriaServicoId,
    string CategoriaNome,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? PrecoBase,
    int? DuracaoEstimadaMinutos,
    bool EhAtivo);

public sealed record ServicoDetalheResponse(
    Guid Id,
    Guid CategoriaServicoId,
    string CategoriaNome,
    string Nome,
    string? Descricao,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? PrecoBase,
    int? DuracaoEstimadaMinutos,
    int Ordem,
    DateTime CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    bool EhAtivo);

public sealed record ServicoSelecaoResponse(
    Guid Id,
    string Nome,
    string CategoriaNome,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? PrecoBase,
    int? DuracaoEstimadaMinutos,
    bool EhAtivo);

public sealed record SalvarPacoteRequest(string Nome, string? Descricao, TipoPrecificacaoCatalogo TipoPrecificacao, decimal? Preco, IReadOnlyCollection<Guid> ServicoIds);

public sealed record PacoteListaResponse(
    Guid Id,
    string Nome,
    int QuantidadeServicos,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? Preco,
    decimal? SomaServicos,
    decimal? Economia,
    int? DuracaoEstimadaMinutos,
    bool EhAtivo);

public sealed record PacoteServicoResponse(
    Guid ServicoId,
    string Nome,
    string CategoriaNome,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? PrecoBase,
    int? DuracaoEstimadaMinutos,
    int Ordem,
    bool EhAtivo);

public sealed record PacoteDetalheResponse(
    Guid Id,
    string Nome,
    string? Descricao,
    TipoPrecificacaoCatalogo TipoPrecificacao,
    decimal? Preco,
    decimal? SomaServicos,
    decimal? Economia,
    int? DuracaoEstimadaMinutos,
    DateTime CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    bool EhAtivo,
    IReadOnlyCollection<PacoteServicoResponse> Servicos);
