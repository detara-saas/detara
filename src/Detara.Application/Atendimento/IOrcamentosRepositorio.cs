using Detara.Application.Abstracoes;
using Detara.Domain.Atendimento;
using Detara.Domain.Catalogo;

namespace Detara.Application.Atendimento;

public sealed record FiltroOrcamentos(int Pagina, int TamanhoPagina, StatusEfetivoOrcamento? Status, string? Pesquisa, DateOnly HojeLocal);
public sealed record OrcamentoListaResultado(Guid Id, string? Codigo, string ClienteNome, string VeiculoDescricao, string VeiculoPlaca,
    DateTime? EmitidoEmUtc, DateOnly ValidoAte, decimal Total, StatusOrcamento Status);
public sealed record OrcamentoItemResultado(Guid Id, TipoItemOrcamento TipoItem, Guid? ItemCatalogoId, string Nome, string? Descricao,
    TipoPrecificacao? TipoPrecificacaoReferencia, decimal? PrecoReferencia, decimal ValorUnitario, int Quantidade, int Ordem, string? Observacao);
public sealed record HistoricoStatusOrcamentoResultado(Guid Id, StatusOrcamento Status, DateTime DataUtc, Guid UsuarioId, string? Observacao);
public sealed record ReferenciaOrcamentoResultado(Guid Id, string? Codigo, StatusOrcamento Status, DateOnly ValidoAte);
public sealed record OrcamentoDetalheResultado(Guid Id, string? Codigo, Guid ClienteId, string ClienteNome, string? ClienteDocumento,
    string? ClienteTelefone, Guid VeiculoId, string VeiculoDescricao, string VeiculoPlaca, Guid? AgendamentoOrigemId,
    Guid? OrcamentoOrigemId, StatusOrcamento Status, DateOnly ValidoAte, string? ObservacaoCliente, string? ObservacaoInterna,
    string? Condicoes, decimal Desconto, decimal Acrescimo, DateTime CriadoEmUtc, DateTime? AtualizadoEmUtc, DateTime? EmitidoEmUtc,
    DateTime? AprovadoEmUtc, DateTime? RecusadoEmUtc, DateTime? CanceladoEmUtc, DateTime? SubstituidoEmUtc,
    Guid? AprovadoPorUsuarioId, IReadOnlyCollection<OrcamentoItemResultado> Itens,
    IReadOnlyCollection<HistoricoStatusOrcamentoResultado> Historico, ReferenciaOrcamentoResultado? Origem,
    ReferenciaOrcamentoResultado? Substituto);

public interface IOrcamentosRepositorio
{
    Task<PaginacaoResultado<OrcamentoListaResultado>> ListarAsync(FiltroOrcamentos filtro, CancellationToken cancellationToken);
    Task<OrcamentoDetalheResultado?> ObterDetalheAsync(Guid id, CancellationToken cancellationToken);
    Task<Orcamento?> ObterParaAlteracaoAsync(Guid id, CancellationToken cancellationToken);
    void Adicionar(Orcamento orcamento);
    void RemoverItensAtuais(Orcamento orcamento);
    void AdicionarItensAtuais(Orcamento orcamento);
    void AdicionarUltimoHistorico(Orcamento orcamento);
    Task SalvarAsync(CancellationToken cancellationToken);
}
