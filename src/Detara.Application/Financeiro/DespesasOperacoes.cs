using Detara.Application.Abstracoes;
using Detara.Application.Agenda;
using Detara.Domain.Financeiro;
using FluentValidation;
using MediatR;

namespace Detara.Application.Financeiro;

public enum FiltroStatusDespesa { Pendente = 1, Pago = 2, Cancelado = 3, Vencido = 4 }
public sealed record DadosDespesa(string Descricao, Guid CategoriaId, decimal Valor, DateOnly Competencia,
    DateOnly Vencimento, string? Fornecedor, string? Observacao);
public sealed record DadosRecorrencia(string Descricao, Guid CategoriaId, decimal Valor, int DiaVencimento,
    DateOnly CompetenciaInicial, DateOnly? CompetenciaFinal, string? Fornecedor, string? Observacao);
public sealed record ListarDespesasQuery(DateOnly? Competencia = null, int Pagina = 1, int TamanhoPagina = 25,
    FiltroStatusDespesa? Status = null, Guid? CategoriaId = null, OrigemContaPagar? Origem = null, string? Pesquisa = null)
    : IRequest<DespesasResultado>;
public sealed record DespesaListaResultado(Guid Id, string Descricao, Guid CategoriaId, string Categoria,
    OrigemContaPagar Origem, Guid? RecorrenciaId, DateOnly Competencia, DateOnly Vencimento, decimal Valor,
    StatusContaPagar Status, bool Vencida, DateOnly? DataPagamento, decimal? ValorPago,
    string? Fornecedor, string? Observacao, long Versao);
public sealed record ResumoDespesasResultado(decimal Total, decimal Pago, decimal APagar, decimal Vencido);
public sealed record DespesasResultado(PaginacaoResultado<DespesaListaResultado> Contas,
    ResumoDespesasResultado Resumo, DateOnly Competencia, DateOnly Hoje);
public sealed record RecorrenciaListaResultado(Guid Id, string Descricao, Guid CategoriaId, string Categoria,
    decimal Valor, int DiaVencimento, DateOnly CompetenciaInicial, DateOnly? CompetenciaFinal,
    DateOnly ProximaCompetencia, bool Ativa, string? Fornecedor, string? Observacao, long Versao);
public sealed record CategoriaDespesaResultado(Guid Id, string Nome, bool Ativa, long Versao);
public sealed record ObterDespesaQuery(Guid Id) : IRequest<ContaPagar>;
public sealed record CriarDespesaCommand(DadosDespesa Dados) : IRequest<Guid>;
public sealed record EditarDespesaCommand(Guid Id, long Versao, DadosDespesa Dados) : IRequest<Guid>;
public sealed record PagarDespesaCommand(Guid Id, long Versao, DateOnly DataPagamento, decimal ValorPago) : IRequest<Guid>;
public sealed record EstornarDespesaCommand(Guid Id, long Versao, string Motivo) : IRequest<Guid>;
public sealed record CancelarDespesaCommand(Guid Id, long Versao) : IRequest<Guid>;
public sealed record ListarRecorrenciasQuery(int Pagina = 1, int TamanhoPagina = 25) : IRequest<PaginacaoResultado<RecorrenciaListaResultado>>;
public sealed record ObterRecorrenciaQuery(Guid Id) : IRequest<DespesaRecorrente>;
public sealed record CriarRecorrenciaCommand(DadosRecorrencia Dados) : IRequest<Guid>;
public sealed record EditarRecorrenciaCommand(Guid Id, long Versao, DadosRecorrencia Dados) : IRequest<Guid>;
public sealed record AtividadeRecorrenciaCommand(Guid Id, long Versao, bool Ativa) : IRequest<Guid>;
public sealed record ListarCategoriasDespesaQuery : IRequest<IReadOnlyCollection<CategoriaDespesaResultado>>;
public sealed record CriarCategoriaDespesaCommand(string Nome) : IRequest<Guid>;
public sealed record EditarCategoriaDespesaCommand(Guid Id, long Versao, string Nome, bool Ativa) : IRequest<Guid>;

public interface IDespesasRepositorio
{
    Task<DespesasResultado> ListarAsync(ListarDespesasQuery filtro, DateOnly hoje, CancellationToken ct);
    Task<ContaPagar?> ObterContaAsync(Guid id, CancellationToken ct);
    Task<DespesaRecorrente?> ObterRecorrenciaAsync(Guid id, CancellationToken ct);
    Task<CategoriaDespesa?> ObterCategoriaAsync(Guid id, CancellationToken ct);
    Task<bool> CategoriaPossuiRecorrenciasAtivasAsync(Guid id, CancellationToken ct);
    Task<PaginacaoResultado<RecorrenciaListaResultado>> ListarRecorrenciasAsync(int pagina, int tamanho, CancellationToken ct);
    Task<IReadOnlyCollection<CategoriaDespesaResultado>> ListarCategoriasAsync(CancellationToken ct);
    void Adicionar(ContaPagar conta);
    void Adicionar(DespesaRecorrente regra);
    void Adicionar(CategoriaDespesa categoria);
    void Adicionar(PagamentoContaPagar pagamento);
    Task SalvarAsync(CancellationToken ct);
}

internal sealed class ListarDespesasValidator : AbstractValidator<ListarDespesasQuery>
{
    public ListarDespesasValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 1000000);
        RuleFor(x => x.TamanhoPagina).Must(x => x is 10 or 25 or 50);
        RuleFor(x => x.Competencia).Must(x => x == null || x.Value.Day == 1 && x.Value.Year is >= 2000 and <= 9998);
        RuleFor(x => x.Status).IsInEnum();
        RuleFor(x => x.Origem).IsInEnum();
        RuleFor(x => x.Pesquisa).MaximumLength(200);
    }
}
internal sealed class ListarRecorrenciasValidator : AbstractValidator<ListarRecorrenciasQuery>
{
    public ListarRecorrenciasValidator()
    {
        RuleFor(x => x.Pagina).InclusiveBetween(1, 1000000);
        RuleFor(x => x.TamanhoPagina).Must(x => x is 10 or 25 or 50);
    }
}
internal sealed class DadosDespesaValidator : AbstractValidator<DadosDespesa>
{
    public DadosDespesaValidator()
    {
        RuleFor(x => x.Descricao).NotEmpty().MaximumLength(200);
        RuleFor(x => x.CategoriaId).NotEmpty();
        RuleFor(x => x.Valor).GreaterThan(0).PrecisionScale(18, 2, true);
        RuleFor(x => x.Competencia).Must(x => x.Day == 1 && x.Year is >= 2000 and <= 9998);
        RuleFor(x => x.Vencimento).NotEmpty();
        RuleFor(x => x.Fornecedor).MaximumLength(160);
        RuleFor(x => x.Observacao).MaximumLength(2000);
    }
}
internal sealed class CriarDespesaValidator : AbstractValidator<CriarDespesaCommand>
{
    public CriarDespesaValidator() => RuleFor(x => x.Dados).NotNull().SetValidator(new DadosDespesaValidator());
}
internal sealed class EditarDespesaValidator : AbstractValidator<EditarDespesaCommand>
{
    public EditarDespesaValidator() { RuleFor(x => x.Id).NotEmpty(); RuleFor(x => x.Versao).GreaterThan(0); RuleFor(x => x.Dados).NotNull().SetValidator(new DadosDespesaValidator()); }
}

internal sealed class DespesasHandlers(IDespesasRepositorio repo, IUsuarioContexto usuario,
    IPlataformaFinanceiroConsulta plataforma, IConversorFusoHorario conversor, TimeProvider relogio) :
    IRequestHandler<ListarDespesasQuery, DespesasResultado>, IRequestHandler<ObterDespesaQuery, ContaPagar>,
    IRequestHandler<CriarDespesaCommand, Guid>, IRequestHandler<EditarDespesaCommand, Guid>,
    IRequestHandler<PagarDespesaCommand, Guid>, IRequestHandler<EstornarDespesaCommand, Guid>,
    IRequestHandler<CancelarDespesaCommand, Guid>, IRequestHandler<ListarRecorrenciasQuery, PaginacaoResultado<RecorrenciaListaResultado>>,
    IRequestHandler<ObterRecorrenciaQuery, DespesaRecorrente>, IRequestHandler<CriarRecorrenciaCommand, Guid>,
    IRequestHandler<EditarRecorrenciaCommand, Guid>, IRequestHandler<AtividadeRecorrenciaCommand, Guid>,
    IRequestHandler<ListarCategoriasDespesaQuery, IReadOnlyCollection<CategoriaDespesaResultado>>,
    IRequestHandler<CriarCategoriaDespesaCommand, Guid>, IRequestHandler<EditarCategoriaDespesaCommand, Guid>
{
    private async Task<DateOnly> Hoje(CancellationToken ct) => DateOnly.FromDateTime(conversor.ParaLocal(relogio.GetUtcNow().UtcDateTime,
        await FinanceiroFluxo.ObterFusoAsync(plataforma, usuario.EmpresaId, ct)));
    private async Task<CategoriaDespesa> Categoria(Guid id, CancellationToken ct) => await repo.ObterCategoriaAsync(id, ct)
        ?? throw new RecursoNaoEncontradoException("Categoria não encontrada.");
    public async Task<ContaPagar> Handle(ObterDespesaQuery q, CancellationToken ct) => await repo.ObterContaAsync(q.Id, ct)
        ?? throw new RecursoNaoEncontradoException("Despesa não encontrada.");
    public async Task<DespesaRecorrente> Handle(ObterRecorrenciaQuery q, CancellationToken ct) => await repo.ObterRecorrenciaAsync(q.Id, ct)
        ?? throw new RecursoNaoEncontradoException("Recorrência não encontrada.");
    private static void Versao(long atual, long informada)
    { if (atual != informada) throw new ConflitoRegraNegocioException("O registro mudou. Atualize a página antes de tentar novamente."); }
    public async Task<DespesasResultado> Handle(ListarDespesasQuery q, CancellationToken ct) => await repo.ListarAsync(q, await Hoje(ct), ct);
    public Task<PaginacaoResultado<RecorrenciaListaResultado>> Handle(ListarRecorrenciasQuery q, CancellationToken ct) => repo.ListarRecorrenciasAsync(q.Pagina, q.TamanhoPagina, ct);
    public Task<IReadOnlyCollection<CategoriaDespesaResultado>> Handle(ListarCategoriasDespesaQuery q, CancellationToken ct) => repo.ListarCategoriasAsync(ct);
    public async Task<Guid> Handle(CriarDespesaCommand q, CancellationToken ct)
    {
        var d = q.Dados;
        var categoria = await Categoria(d.CategoriaId, ct);
        ContaPagar conta = null!;
        FinanceiroFluxo.ExecutarRegra(() => conta = new(usuario.EmpresaId, d.Descricao, categoria, d.Valor, d.Competencia, d.Vencimento, d.Fornecedor, d.Observacao));
        repo.Adicionar(conta); await repo.SalvarAsync(ct); return conta.Id;
    }
    public async Task<Guid> Handle(EditarDespesaCommand q, CancellationToken ct)
    {
        var conta = await Handle(new ObterDespesaQuery(q.Id), ct); Versao(conta.Versao, q.Versao);
        var d = q.Dados; var categoria = await Categoria(d.CategoriaId, ct);
        FinanceiroFluxo.ExecutarRegra(() => conta.Editar(d.Descricao, categoria, d.Valor, d.Competencia, d.Vencimento, d.Fornecedor, d.Observacao));
        await repo.SalvarAsync(ct); return conta.Id;
    }
    public async Task<Guid> Handle(PagarDespesaCommand q, CancellationToken ct)
    {
        var conta = await Handle(new ObterDespesaQuery(q.Id), ct); Versao(conta.Versao, q.Versao);
        if (q.DataPagamento > await Hoje(ct)) throw new ArgumentException("O pagamento não pode ter data futura.");
        FinanceiroFluxo.ExecutarRegra(() => repo.Adicionar(conta.RegistrarPagamento(q.DataPagamento, q.ValorPago, usuario.UsuarioId, relogio.GetUtcNow().UtcDateTime)));
        await repo.SalvarAsync(ct); return conta.Id;
    }
    public async Task<Guid> Handle(EstornarDespesaCommand q, CancellationToken ct)
    {
        var conta = await Handle(new ObterDespesaQuery(q.Id), ct); Versao(conta.Versao, q.Versao);
        FinanceiroFluxo.ExecutarRegra(() => conta.EstornarPagamento(usuario.UsuarioId, q.Motivo, relogio.GetUtcNow().UtcDateTime));
        await repo.SalvarAsync(ct); return conta.Id;
    }
    public async Task<Guid> Handle(CancelarDespesaCommand q, CancellationToken ct)
    {
        var conta = await Handle(new ObterDespesaQuery(q.Id), ct); Versao(conta.Versao, q.Versao);
        FinanceiroFluxo.ExecutarRegra(() => conta.Cancelar(usuario.UsuarioId, relogio.GetUtcNow().UtcDateTime));
        await repo.SalvarAsync(ct); return conta.Id;
    }
    public async Task<Guid> Handle(CriarRecorrenciaCommand q, CancellationToken ct)
    {
        var d = q.Dados ?? throw new ArgumentException("Preencha a recorrência.");
        var categoria = await Categoria(d.CategoriaId, ct); var hoje = await Hoje(ct);
        DespesaRecorrente regra = null!;
        FinanceiroFluxo.ExecutarRegra(() => regra = new(usuario.EmpresaId, d.Descricao, categoria, d.Valor, d.DiaVencimento,
            d.CompetenciaInicial, d.CompetenciaFinal, hoje, d.Fornecedor, d.Observacao));
        repo.Adicionar(regra);
        if (regra.PodeMaterializar(hoje)) repo.Adicionar(regra.Materializar(categoria, hoje));
        await repo.SalvarAsync(ct); return regra.Id;
    }
    public async Task<Guid> Handle(EditarRecorrenciaCommand q, CancellationToken ct)
    {
        var regra = await Handle(new ObterRecorrenciaQuery(q.Id), ct); Versao(regra.Versao, q.Versao);
        var d = q.Dados ?? throw new ArgumentException("Preencha a recorrência.");
        if (d.CompetenciaInicial != regra.CompetenciaInicial) throw new ArgumentException("A competência inicial não pode ser alterada.");
        var categoria = await Categoria(d.CategoriaId, ct);
        FinanceiroFluxo.ExecutarRegra(() => regra.Editar(d.Descricao, categoria, d.Valor, d.DiaVencimento, d.CompetenciaFinal, d.Fornecedor, d.Observacao));
        await repo.SalvarAsync(ct); return regra.Id;
    }
    public async Task<Guid> Handle(AtividadeRecorrenciaCommand q, CancellationToken ct)
    {
        var regra = await Handle(new ObterRecorrenciaQuery(q.Id), ct); Versao(regra.Versao, q.Versao);
        if (q.Ativa && !(await Categoria(regra.CategoriaDespesaId, ct)).EhAtivo)
            throw new ConflitoRegraNegocioException("Escolha uma categoria ativa antes de reativar a recorrência.");
        var hoje = await Hoje(ct); regra.DefinirAtividade(q.Ativa, hoje);
        if (regra.PodeMaterializar(hoje))
        {
            var categoria = await Categoria(regra.CategoriaDespesaId, ct);
            FinanceiroFluxo.ExecutarRegra(() => repo.Adicionar(regra.Materializar(categoria, hoje)));
        }
        await repo.SalvarAsync(ct); return regra.Id;
    }
    public async Task<Guid> Handle(CriarCategoriaDespesaCommand q, CancellationToken ct)
    { var categoria = new CategoriaDespesa(usuario.EmpresaId, q.Nome); repo.Adicionar(categoria); await repo.SalvarAsync(ct); return categoria.Id; }
    public async Task<Guid> Handle(EditarCategoriaDespesaCommand q, CancellationToken ct)
    {
        var categoria = await Categoria(q.Id, ct); Versao(categoria.Versao, q.Versao);
        if (!q.Ativa && await repo.CategoriaPossuiRecorrenciasAtivasAsync(q.Id, ct))
            throw new ConflitoRegraNegocioException("Inative ou altere as recorrências desta categoria antes de inativá-la.");
        categoria.Renomear(q.Nome); categoria.DefinirAtividade(q.Ativa); await repo.SalvarAsync(ct); return categoria.Id;
    }
}
