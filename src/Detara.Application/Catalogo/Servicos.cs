using Detara.Application.Abstracoes;
using Detara.Domain.Atendimento;
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
public sealed record ObterServicoQuery(Guid Id) : IRequest<ServicoDetalheVisualizacao>;
public sealed record CriarServicoCommand(Guid CategoriaServicoId, string Nome, string? Descricao, TipoPrecificacao TipoPrecificacao, decimal? PrecoBase, int? DuracaoEstimadaMinutos, int Ordem) : IRequest<ServicoDetalheResultado>;
public sealed record AtualizarServicoCommand(Guid Id, Guid CategoriaServicoId, string Nome, string? Descricao, TipoPrecificacao TipoPrecificacao, decimal? PrecoBase, int? DuracaoEstimadaMinutos, int Ordem) : IRequest<ServicoDetalheResultado>;
public sealed record AlterarStatusServicoCommand(Guid Id, bool EhAtivo) : IRequest;

internal sealed class CriarServicoValidator : AbstractValidator<CriarServicoCommand>
{
    public CriarServicoValidator()
    {
        RuleFor(x => x.CategoriaServicoId).NotEmpty()
            .WithMessage("Selecione uma categoria para o serviço.");
        RuleFor(x => x.Nome).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => x.Descricao).MaximumLength(2000);
        RuleFor(x => x.TipoPrecificacao).IsInEnum();
        RuleFor(x => x.PrecoBase).NotNull().GreaterThanOrEqualTo(0)
            .When(x => x.TipoPrecificacao is TipoPrecificacao.Fixo or TipoPrecificacao.APartirDe);
        RuleFor(x => x.PrecoBase).Null()
            .When(x => x.TipoPrecificacao == TipoPrecificacao.SobConsulta);
        RuleFor(x => x.DuracaoEstimadaMinutos).InclusiveBetween(1, 43200)
            .When(x => x.DuracaoEstimadaMinutos.HasValue);
        RuleFor(x => x.Ordem).GreaterThanOrEqualTo(0);
    }
}

internal sealed class AtualizarServicoValidator : AbstractValidator<AtualizarServicoCommand>
{
    public AtualizarServicoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.CategoriaServicoId).NotEmpty()
            .WithMessage("Selecione uma categoria para o serviço.");
        RuleFor(x => x.Nome).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => x.Descricao).MaximumLength(2000);
        RuleFor(x => x.TipoPrecificacao).IsInEnum();
        RuleFor(x => x.PrecoBase).NotNull().GreaterThanOrEqualTo(0)
            .When(x => x.TipoPrecificacao is TipoPrecificacao.Fixo or TipoPrecificacao.APartirDe);
        RuleFor(x => x.PrecoBase).Null()
            .When(x => x.TipoPrecificacao == TipoPrecificacao.SobConsulta);
        RuleFor(x => x.DuracaoEstimadaMinutos).InclusiveBetween(1, 43200)
            .When(x => x.DuracaoEstimadaMinutos.HasValue);
        RuleFor(x => x.Ordem).GreaterThanOrEqualTo(0);
    }
}
internal sealed class ListarServicosValidator : AbstractValidator<ListarServicosQuery> { public ListarServicosValidator() { RuleFor(x => x.Filtro.Pagina).GreaterThanOrEqualTo(1); RuleFor(x => x.Filtro.TamanhoPagina).Must(x => x is 10 or 25 or 50); RuleFor(x => x.Filtro.Pesquisa).MaximumLength(160); } }

internal sealed class ListarServicosHandler(IServicosRepositorio repositorio) : IRequestHandler<ListarServicosQuery, PaginacaoResultado<ServicoListaItemResultado>> { public Task<PaginacaoResultado<ServicoListaItemResultado>> Handle(ListarServicosQuery request, CancellationToken cancellationToken) => repositorio.ListarAsync(request.Filtro, cancellationToken); }
internal sealed class ListarServicosSelecaoHandler(IServicosRepositorio repositorio) : IRequestHandler<ListarServicosSelecaoQuery, IReadOnlyCollection<ServicoSelecaoResultado>> { public Task<IReadOnlyCollection<ServicoSelecaoResultado>> Handle(ListarServicosSelecaoQuery request, CancellationToken cancellationToken) => repositorio.ListarParaSelecaoAsync(request.IncluirInativos, cancellationToken); }
internal sealed class ObterServicoHandler(IUsuarioContexto usuario, IServicosRepositorio repositorio,
    IHistoricoExecucoesCatalogoConsulta historico)
    : IRequestHandler<ObterServicoQuery, ServicoDetalheVisualizacao>
{
    public async Task<ServicoDetalheVisualizacao> Handle(ObterServicoQuery request, CancellationToken cancellationToken)
    {
        var servico = await repositorio.ObterDetalheAsync(request.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Serviço não encontrado.");
        var execucoes = await historico.ListarAsync(usuario.EmpresaId, TipoItemOrcamento.Servico,
            request.Id, 10, cancellationToken);
        return new(servico, execucoes);
    }
}

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
