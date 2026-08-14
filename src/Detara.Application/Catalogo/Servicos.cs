using Detara.Application.Abstracoes;
using Detara.Domain.Catalogo;
using Detara.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace Detara.Application.Catalogo;

public sealed record FiltroServicos(int Pagina, int TamanhoPagina, string? Pesquisa, bool? EhAtivo, Guid? CategoriaServicoId);
public sealed record ServicoListaItemResultado(Guid Id, string Nome, Guid CategoriaServicoId, string CategoriaNome, TipoPrecificacao TipoPrecificacao, decimal? PrecoBase, int? DuracaoEstimadaMinutos, bool EhAtivo);
public sealed record ServicoDetalheResultado(Guid Id, Guid CategoriaServicoId, string CategoriaNome, string Nome, string? Descricao, TipoPrecificacao TipoPrecificacao, decimal? PrecoBase, int? DuracaoEstimadaMinutos, int Ordem, DateTime CriadoEmUtc, DateTime? AtualizadoEmUtc, bool EhAtivo);
public sealed record ServicoSelecaoResultado(Guid Id, string Nome, string CategoriaNome, TipoPrecificacao TipoPrecificacao, decimal? PrecoBase, int? DuracaoEstimadaMinutos, bool EhAtivo);
public sealed record ListarServicosQuery(FiltroServicos Filtro) : IRequest<PaginacaoResultado<ServicoListaItemResultado>>;
public sealed record ListarServicosSelecaoQuery(bool IncluirInativos = false) : IRequest<IReadOnlyCollection<ServicoSelecaoResultado>>;
public sealed record ObterServicoQuery(Guid Id) : IRequest<ServicoDetalheResultado>;
public sealed record CriarServicoCommand(Guid CategoriaServicoId, string Nome, string? Descricao, TipoPrecificacao TipoPrecificacao, decimal? PrecoBase, int? DuracaoEstimadaMinutos, int Ordem) : IRequest<ServicoDetalheResultado>;
public sealed record AtualizarServicoCommand(Guid Id, Guid CategoriaServicoId, string Nome, string? Descricao, TipoPrecificacao TipoPrecificacao, decimal? PrecoBase, int? DuracaoEstimadaMinutos, int Ordem) : IRequest<ServicoDetalheResultado>;
public sealed record AlterarStatusServicoCommand(Guid Id, bool EhAtivo) : IRequest;

internal abstract class ServicoValidatorBase<T> : AbstractValidator<T>
{
    protected void Regras(Func<T, Guid> categoria, Func<T, string> nome, Func<T, string?> descricao, Func<T, TipoPrecificacao> tipo, Func<T, decimal?> preco, Func<T, int?> duracao, Func<T, int> ordem)
    {
        RuleFor(x => categoria(x)).NotEmpty(); RuleFor(x => nome(x)).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => descricao(x)).MaximumLength(2000); RuleFor(x => tipo(x)).IsInEnum();
        RuleFor(x => preco(x)).NotNull().GreaterThanOrEqualTo(0).When(x => tipo(x) is TipoPrecificacao.Fixo or TipoPrecificacao.APartirDe);
        RuleFor(x => preco(x)).Null().When(x => tipo(x) == TipoPrecificacao.SobConsulta);
        RuleFor(x => duracao(x)).InclusiveBetween(1, 43200).When(x => duracao(x).HasValue); RuleFor(x => ordem(x)).GreaterThanOrEqualTo(0);
    }
}
internal sealed class CriarServicoValidator : ServicoValidatorBase<CriarServicoCommand> { public CriarServicoValidator() => Regras(x => x.CategoriaServicoId, x => x.Nome, x => x.Descricao, x => x.TipoPrecificacao, x => x.PrecoBase, x => x.DuracaoEstimadaMinutos, x => x.Ordem); }
internal sealed class AtualizarServicoValidator : ServicoValidatorBase<AtualizarServicoCommand> { public AtualizarServicoValidator() { RuleFor(x => x.Id).NotEmpty(); Regras(x => x.CategoriaServicoId, x => x.Nome, x => x.Descricao, x => x.TipoPrecificacao, x => x.PrecoBase, x => x.DuracaoEstimadaMinutos, x => x.Ordem); } }
internal sealed class ListarServicosValidator : AbstractValidator<ListarServicosQuery> { public ListarServicosValidator() { RuleFor(x => x.Filtro.Pagina).GreaterThanOrEqualTo(1); RuleFor(x => x.Filtro.TamanhoPagina).Must(x => x is 10 or 25 or 50); RuleFor(x => x.Filtro.Pesquisa).MaximumLength(160); } }

internal sealed class ListarServicosHandler(IServicosRepositorio repositorio) : IRequestHandler<ListarServicosQuery, PaginacaoResultado<ServicoListaItemResultado>> { public Task<PaginacaoResultado<ServicoListaItemResultado>> Handle(ListarServicosQuery request, CancellationToken cancellationToken) => repositorio.ListarAsync(request.Filtro, cancellationToken); }
internal sealed class ListarServicosSelecaoHandler(IServicosRepositorio repositorio) : IRequestHandler<ListarServicosSelecaoQuery, IReadOnlyCollection<ServicoSelecaoResultado>> { public Task<IReadOnlyCollection<ServicoSelecaoResultado>> Handle(ListarServicosSelecaoQuery request, CancellationToken cancellationToken) => repositorio.ListarParaSelecaoAsync(request.IncluirInativos, cancellationToken); }
internal sealed class ObterServicoHandler(IServicosRepositorio repositorio) : IRequestHandler<ObterServicoQuery, ServicoDetalheResultado> { public async Task<ServicoDetalheResultado> Handle(ObterServicoQuery request, CancellationToken cancellationToken) => await repositorio.ObterDetalheAsync(request.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Serviço não encontrado."); }

internal sealed class CriarServicoHandler(IUsuarioContexto usuario, ICategoriasServicoRepositorio categorias, IServicosRepositorio servicos) : IRequestHandler<CriarServicoCommand, ServicoDetalheResultado>
{
    public async Task<ServicoDetalheResultado> Handle(CriarServicoCommand request, CancellationToken cancellationToken)
    {
        await ValidarRelacionamentosAsync(usuario, categorias, servicos, request.CategoriaServicoId, request.Nome, null, cancellationToken);
        var servico = new Servico(usuario.EmpresaId, request.CategoriaServicoId, request.Nome, request.Descricao, request.TipoPrecificacao, request.PrecoBase, request.DuracaoEstimadaMinutos, request.Ordem);
        servicos.Adicionar(servico); await servicos.SalvarAsync(cancellationToken);
        return await servicos.ObterDetalheAsync(servico.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Serviço não encontrado após o cadastro.");
    }

    internal static async Task ValidarRelacionamentosAsync(IUsuarioContexto usuario, ICategoriasServicoRepositorio categorias, IServicosRepositorio servicos, Guid categoriaId, string nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        if (!await categorias.PertenceAoTenantEAtivaAsync(categoriaId, usuario.EmpresaId, cancellationToken)) throw new RecursoNaoEncontradoException("Categoria não encontrada, inativa ou fora da empresa atual.");
        if (await servicos.NomeEmUsoAsync(categoriaId, nome.Trim(), ignorarId, cancellationToken)) throw new ConflitoRegraNegocioException("Já existe um serviço com este nome na categoria selecionada.");
    }
}

internal sealed class AtualizarServicoHandler(IUsuarioContexto usuario, ICategoriasServicoRepositorio categorias, IServicosRepositorio servicos) : IRequestHandler<AtualizarServicoCommand, ServicoDetalheResultado>
{
    public async Task<ServicoDetalheResultado> Handle(AtualizarServicoCommand request, CancellationToken cancellationToken)
    {
        var servico = await servicos.ObterParaAlteracaoAsync(request.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Serviço não encontrado.");
        await CriarServicoHandler.ValidarRelacionamentosAsync(usuario, categorias, servicos, request.CategoriaServicoId, request.Nome, request.Id, cancellationToken);
        servico.Atualizar(request.CategoriaServicoId, request.Nome, request.Descricao, request.TipoPrecificacao, request.PrecoBase, request.DuracaoEstimadaMinutos, request.Ordem);
        await servicos.SalvarAsync(cancellationToken);
        return await servicos.ObterDetalheAsync(servico.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Serviço não encontrado após a atualização.");
    }
}
internal sealed class AlterarStatusServicoHandler(IServicosRepositorio repositorio) : IRequestHandler<AlterarStatusServicoCommand> { public async Task Handle(AlterarStatusServicoCommand request, CancellationToken cancellationToken) { var item = await repositorio.ObterParaAlteracaoAsync(request.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Serviço não encontrado."); if (request.EhAtivo) item.Ativar(); else item.Desativar(); await repositorio.SalvarAsync(cancellationToken); } }
