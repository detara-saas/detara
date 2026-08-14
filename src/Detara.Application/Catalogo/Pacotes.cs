using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace Detara.Application.Catalogo;

public sealed record FiltroPacotes(int Pagina, int TamanhoPagina, string? Pesquisa, bool? EhAtivo);
public sealed record PacoteListaItemResultado(Guid Id, string Nome, int QuantidadeServicos, decimal? Preco, decimal? SomaServicos, decimal? Economia, int? DuracaoEstimadaMinutos, bool EhAtivo);
public sealed record PacoteServicoResultado(Guid ServicoId, string Nome, string CategoriaNome, decimal? PrecoBase, int? DuracaoEstimadaMinutos, int Ordem, bool EhAtivo);
public sealed record PacoteDetalheResultado(Guid Id, string Nome, string? Descricao, decimal? Preco, decimal? SomaServicos, decimal? Economia, int? DuracaoEstimadaMinutos, DateTime CriadoEmUtc, DateTime? AtualizadoEmUtc, bool EhAtivo, IReadOnlyCollection<PacoteServicoResultado> Servicos);
public sealed record ListarPacotesQuery(FiltroPacotes Filtro) : IRequest<PaginacaoResultado<PacoteListaItemResultado>>;
public sealed record ObterPacoteQuery(Guid Id) : IRequest<PacoteDetalheResultado>;
public sealed record CriarPacoteCommand(string Nome, string? Descricao, decimal? Preco, IReadOnlyCollection<Guid> ServicoIds) : IRequest<PacoteDetalheResultado>;
public sealed record AtualizarPacoteCommand(Guid Id, string Nome, string? Descricao, decimal? Preco, IReadOnlyCollection<Guid> ServicoIds) : IRequest<PacoteDetalheResultado>;
public sealed record AlterarStatusPacoteCommand(Guid Id, bool EhAtivo) : IRequest;

internal abstract class PacoteValidatorBase<T> : AbstractValidator<T>
{
    protected void Regras(Func<T, string> nome, Func<T, string?> descricao, Func<T, decimal?> preco, Func<T, IReadOnlyCollection<Guid>> servicos)
    {
        RuleFor(x => nome(x)).NotEmpty().MinimumLength(2).MaximumLength(160);
        RuleFor(x => descricao(x)).MaximumLength(2000);
        RuleFor(x => preco(x)).GreaterThanOrEqualTo(0).When(x => preco(x).HasValue);
        RuleFor(x => servicos(x)).NotEmpty().Must(x => x.Distinct().Count() == x.Count).WithMessage("Os serviços não podem se repetir.");
        RuleForEach(x => servicos(x)).NotEmpty();
    }
}
internal sealed class CriarPacoteValidator : PacoteValidatorBase<CriarPacoteCommand> { public CriarPacoteValidator() => Regras(x => x.Nome, x => x.Descricao, x => x.Preco, x => x.ServicoIds); }
internal sealed class AtualizarPacoteValidator : PacoteValidatorBase<AtualizarPacoteCommand> { public AtualizarPacoteValidator() { RuleFor(x => x.Id).NotEmpty(); Regras(x => x.Nome, x => x.Descricao, x => x.Preco, x => x.ServicoIds); } }
internal sealed class ListarPacotesValidator : AbstractValidator<ListarPacotesQuery> { public ListarPacotesValidator() { RuleFor(x => x.Filtro.Pagina).GreaterThanOrEqualTo(1); RuleFor(x => x.Filtro.TamanhoPagina).Must(x => x is 10 or 25 or 50); RuleFor(x => x.Filtro.Pesquisa).MaximumLength(160); } }

internal sealed class ListarPacotesHandler(IPacotesRepositorio repositorio) : IRequestHandler<ListarPacotesQuery, PaginacaoResultado<PacoteListaItemResultado>> { public Task<PaginacaoResultado<PacoteListaItemResultado>> Handle(ListarPacotesQuery request, CancellationToken cancellationToken) => repositorio.ListarAsync(request.Filtro, cancellationToken); }
internal sealed class ObterPacoteHandler(IPacotesRepositorio repositorio) : IRequestHandler<ObterPacoteQuery, PacoteDetalheResultado> { public async Task<PacoteDetalheResultado> Handle(ObterPacoteQuery request, CancellationToken cancellationToken) => await repositorio.ObterDetalheAsync(request.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Pacote não encontrado."); }

internal sealed class CriarPacoteHandler(IUsuarioContexto usuario, IServicosRepositorio servicos, IPacotesRepositorio pacotes) : IRequestHandler<CriarPacoteCommand, PacoteDetalheResultado>
{
    public async Task<PacoteDetalheResultado> Handle(CriarPacoteCommand request, CancellationToken cancellationToken)
    {
        await ValidarAsync(usuario, servicos, pacotes, request.Nome, request.ServicoIds, null, cancellationToken);
        var pacote = new Pacote(usuario.EmpresaId, request.Nome, request.Descricao, request.Preco, request.ServicoIds);
        pacotes.Adicionar(pacote); await pacotes.SalvarAsync(cancellationToken);
        return await pacotes.ObterDetalheAsync(pacote.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Pacote não encontrado após o cadastro.");
    }

    internal static async Task ValidarAsync(IUsuarioContexto usuario, IServicosRepositorio servicos, IPacotesRepositorio pacotes, string nome, IReadOnlyCollection<Guid> ids, Guid? ignorarId, CancellationToken cancellationToken)
    {
        if (await pacotes.NomeEmUsoAsync(nome.Trim(), ignorarId, cancellationToken)) throw new ConflitoRegraNegocioException("Já existe um pacote com este nome na empresa atual.");
        var encontrados = await servicos.ObterIdsDoTenantAsync(ids, usuario.EmpresaId, cancellationToken);
        if (encontrados.Count != ids.Distinct().Count()) throw new RecursoNaoEncontradoException("Um ou mais serviços não pertencem à empresa atual.");
    }
}

internal sealed class AtualizarPacoteHandler(IUsuarioContexto usuario, IServicosRepositorio servicos, IPacotesRepositorio pacotes) : IRequestHandler<AtualizarPacoteCommand, PacoteDetalheResultado>
{
    public async Task<PacoteDetalheResultado> Handle(AtualizarPacoteCommand request, CancellationToken cancellationToken)
    {
        var pacote = await pacotes.ObterParaAlteracaoAsync(request.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Pacote não encontrado.");
        await CriarPacoteHandler.ValidarAsync(usuario, servicos, pacotes, request.Nome, request.ServicoIds, request.Id, cancellationToken);
        pacotes.RemoverComposicaoAtual(pacote);
        pacote.Atualizar(request.Nome, request.Descricao, request.Preco, request.ServicoIds);
        pacotes.AdicionarComposicaoAtual(pacote);
        await pacotes.SalvarAsync(cancellationToken);
        return await pacotes.ObterDetalheAsync(pacote.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Pacote não encontrado após a atualização.");
    }
}
internal sealed class AlterarStatusPacoteHandler(IPacotesRepositorio repositorio) : IRequestHandler<AlterarStatusPacoteCommand> { public async Task Handle(AlterarStatusPacoteCommand request, CancellationToken cancellationToken) { var item = await repositorio.ObterParaAlteracaoAsync(request.Id, cancellationToken) ?? throw new RecursoNaoEncontradoException("Pacote não encontrado."); if (request.EhAtivo) item.Ativar(); else item.Desativar(); await repositorio.SalvarAsync(cancellationToken); } }
