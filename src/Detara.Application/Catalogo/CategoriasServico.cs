using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace Detara.Application.Catalogo;

public sealed record CategoriaServicoResultado(Guid Id, string Nome, string? Descricao, int Ordem, int QuantidadeServicos, bool EhAtivo);
public sealed record ListarCategoriasServicoQuery(bool? EhAtivo = null) : IRequest<IReadOnlyCollection<CategoriaServicoResultado>>;
public sealed record CriarCategoriaServicoCommand(string Nome, string? Descricao, int Ordem) : IRequest<CategoriaServicoResultado>;
public sealed record AtualizarCategoriaServicoCommand(Guid Id, string Nome, string? Descricao, int Ordem) : IRequest<CategoriaServicoResultado>;
public sealed record AlterarStatusCategoriaServicoCommand(Guid Id, bool EhAtivo) : IRequest;

internal sealed class CriarCategoriaServicoValidator : AbstractValidator<CriarCategoriaServicoCommand>
{
    public CriarCategoriaServicoValidator()
    {
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Descricao).MaximumLength(1000);
        RuleFor(x => x.Ordem).GreaterThanOrEqualTo(0);
    }
}

internal sealed class AtualizarCategoriaServicoValidator : AbstractValidator<AtualizarCategoriaServicoCommand>
{
    public AtualizarCategoriaServicoValidator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Nome).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Descricao).MaximumLength(1000);
        RuleFor(x => x.Ordem).GreaterThanOrEqualTo(0);
    }
}

internal sealed class AlterarStatusCategoriaServicoValidator : AbstractValidator<AlterarStatusCategoriaServicoCommand>
{
    public AlterarStatusCategoriaServicoValidator() => RuleFor(x => x.Id).NotEmpty();
}

internal sealed class ListarCategoriasServicoHandler(ICategoriasServicoRepositorio repositorio)
    : IRequestHandler<ListarCategoriasServicoQuery, IReadOnlyCollection<CategoriaServicoResultado>>
{
    public Task<IReadOnlyCollection<CategoriaServicoResultado>> Handle(ListarCategoriasServicoQuery request, CancellationToken cancellationToken) =>
        repositorio.ListarAsync(request.EhAtivo, cancellationToken);
}

internal sealed class CriarCategoriaServicoHandler(IUsuarioContexto usuario, ICategoriasServicoRepositorio repositorio)
    : IRequestHandler<CriarCategoriaServicoCommand, CategoriaServicoResultado>
{
    public async Task<CategoriaServicoResultado> Handle(CriarCategoriaServicoCommand request, CancellationToken cancellationToken)
    {
        await ValidarNomeAsync(repositorio, request.Nome.Trim(), null, cancellationToken);
        var categoria = new CategoriaServico(usuario.EmpresaId, request.Nome, request.Descricao, request.Ordem);
        repositorio.Adicionar(categoria);
        await repositorio.SalvarAsync(cancellationToken);
        return new(categoria.Id, categoria.Nome, categoria.Descricao, categoria.Ordem, 0, categoria.EhAtivo);
    }

    internal static async Task ValidarNomeAsync(ICategoriasServicoRepositorio repositorio, string nome, Guid? ignorarId, CancellationToken cancellationToken)
    {
        if (await repositorio.NomeEmUsoAsync(nome, ignorarId, cancellationToken))
            throw new ConflitoRegraNegocioException("Já existe uma categoria com este nome na empresa atual.");
    }
}

internal sealed class AtualizarCategoriaServicoHandler(ICategoriasServicoRepositorio repositorio)
    : IRequestHandler<AtualizarCategoriaServicoCommand, CategoriaServicoResultado>
{
    public async Task<CategoriaServicoResultado> Handle(AtualizarCategoriaServicoCommand request, CancellationToken cancellationToken)
    {
        var categoria = await repositorio.ObterParaAlteracaoAsync(request.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Categoria não encontrada.");
        await CriarCategoriaServicoHandler.ValidarNomeAsync(repositorio, request.Nome.Trim(), request.Id, cancellationToken);
        categoria.Atualizar(request.Nome, request.Descricao, request.Ordem);
        await repositorio.SalvarAsync(cancellationToken);
        return new(categoria.Id, categoria.Nome, categoria.Descricao, categoria.Ordem, categoria.Servicos.Count, categoria.EhAtivo);
    }
}

internal sealed class AlterarStatusCategoriaServicoHandler(ICategoriasServicoRepositorio repositorio)
    : IRequestHandler<AlterarStatusCategoriaServicoCommand>
{
    public async Task Handle(AlterarStatusCategoriaServicoCommand request, CancellationToken cancellationToken)
    {
        var categoria = await repositorio.ObterParaAlteracaoAsync(request.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Categoria não encontrada.");
        if (request.EhAtivo) categoria.Ativar(); else categoria.Desativar();
        await repositorio.SalvarAsync(cancellationToken);
    }
}
