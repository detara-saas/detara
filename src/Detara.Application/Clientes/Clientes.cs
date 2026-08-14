using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace Detara.Application.Clientes;

public sealed record FiltroClientes(
    int Pagina,
    int TamanhoPagina,
    string? Pesquisa,
    bool? EhAtivo,
    TipoPessoa? TipoPessoa,
    string Ordenacao = "nome");

public sealed record ClienteListaItemResultado(
    Guid Id,
    string Nome,
    TipoPessoa TipoPessoa,
    string? CpfCnpj,
    string? Telefone,
    int QuantidadeVeiculos,
    bool EhAtivo);

public sealed record VeiculoResumoClienteResultado(
    Guid Id,
    string Descricao,
    string Placa,
    int? AnoModelo,
    string? Cor,
    int? Quilometragem,
    bool EhAtivo);

public sealed record ClienteDetalheResultado(
    Guid Id,
    string Nome,
    TipoPessoa TipoPessoa,
    string? CpfCnpj,
    string? Telefone,
    string? WhatsApp,
    string? Email,
    DateOnly? DataNascimento,
    string? Observacao,
    DateTime CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    bool EhAtivo,
    IReadOnlyCollection<VeiculoResumoClienteResultado> Veiculos);

public sealed record ClienteBuscaResultado(Guid Id, string Nome, string? Telefone, string? CpfCnpj);

public sealed record ListarClientesQuery(FiltroClientes Filtro)
    : IRequest<PaginacaoResultado<ClienteListaItemResultado>>;

public sealed record BuscarClientesQuery(string Pesquisa, int Limite = 15)
    : IRequest<IReadOnlyCollection<ClienteBuscaResultado>>;

public sealed record ObterClienteQuery(Guid Id) : IRequest<ClienteDetalheResultado>;

public sealed record CriarClienteCommand(
    string Nome,
    string TipoPessoa,
    string? CpfCnpj,
    string? Telefone,
    string? WhatsApp,
    string? Email,
    DateOnly? DataNascimento,
    string? Observacao) : IRequest<ClienteDetalheResultado>;

public sealed record AtualizarClienteCommand(
    Guid Id,
    string Nome,
    string TipoPessoa,
    string? CpfCnpj,
    string? Telefone,
    string? WhatsApp,
    string? Email,
    DateOnly? DataNascimento,
    string? Observacao) : IRequest<ClienteDetalheResultado>;

public sealed record AlterarStatusClienteCommand(Guid Id, bool EhAtivo) : IRequest;

internal sealed class ListarClientesQueryValidator : AbstractValidator<ListarClientesQuery>
{
    public ListarClientesQueryValidator()
    {
        RuleFor(item => item.Filtro.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(item => item.Filtro.TamanhoPagina).Must(item => item is 10 or 25 or 50)
            .WithMessage("O tamanho da página deve ser 10, 25 ou 50.");
        RuleFor(item => item.Filtro.Pesquisa).MaximumLength(160);
        RuleFor(item => item.Filtro.Ordenacao).Must(item => item is "nome" or "criacao")
            .WithMessage("A ordenação deve ser por nome ou criação.");
    }
}

internal sealed class BuscarClientesQueryValidator : AbstractValidator<BuscarClientesQuery>
{
    public BuscarClientesQueryValidator()
    {
        RuleFor(item => item.Pesquisa).NotEmpty().MaximumLength(160);
        RuleFor(item => item.Limite).InclusiveBetween(1, 25);
    }
}

internal sealed class CriarClienteCommandValidator : AbstractValidator<CriarClienteCommand>
{
    public CriarClienteCommandValidator()
    {
        RuleFor(item => item.Nome).NotEmpty().MaximumLength(160);
        RuleFor(item => item.TipoPessoa)
            .Must(TipoPessoaParser.EhValido)
            .WithMessage("O tipo de pessoa deve ser PessoaFisica ou PessoaJuridica.");
        RuleFor(item => item.CpfCnpj).MaximumLength(18);
        RuleFor(item => item.Telefone).MaximumLength(20);
        RuleFor(item => item.WhatsApp).MaximumLength(20);
        RuleFor(item => item.Email).EmailAddress().MaximumLength(200)
            .When(item => !string.IsNullOrWhiteSpace(item.Email));
        RuleFor(item => item.Observacao).MaximumLength(2000);
    }
}

internal sealed class AtualizarClienteCommandValidator : AbstractValidator<AtualizarClienteCommand>
{
    public AtualizarClienteCommandValidator()
    {
        RuleFor(item => item.Id).NotEmpty();
        RuleFor(item => item.Nome).NotEmpty().MaximumLength(160);
        RuleFor(item => item.TipoPessoa).Must(TipoPessoaParser.EhValido)
            .WithMessage("O tipo de pessoa deve ser PessoaFisica ou PessoaJuridica.");
        RuleFor(item => item.CpfCnpj).MaximumLength(18);
        RuleFor(item => item.Telefone).MaximumLength(20);
        RuleFor(item => item.WhatsApp).MaximumLength(20);
        RuleFor(item => item.Email).EmailAddress().MaximumLength(200)
            .When(item => !string.IsNullOrWhiteSpace(item.Email));
        RuleFor(item => item.Observacao).MaximumLength(2000);
    }
}

internal sealed class AlterarStatusClienteCommandValidator : AbstractValidator<AlterarStatusClienteCommand>
{
    public AlterarStatusClienteCommandValidator() => RuleFor(item => item.Id).NotEmpty();
}

internal sealed class ListarClientesQueryHandler(IClientesRepositorio repositorio)
    : IRequestHandler<ListarClientesQuery, PaginacaoResultado<ClienteListaItemResultado>>
{
    public Task<PaginacaoResultado<ClienteListaItemResultado>> Handle(
        ListarClientesQuery request,
        CancellationToken cancellationToken) =>
        repositorio.ListarAsync(request.Filtro, cancellationToken);
}

internal sealed class BuscarClientesQueryHandler(IClientesRepositorio repositorio)
    : IRequestHandler<BuscarClientesQuery, IReadOnlyCollection<ClienteBuscaResultado>>
{
    public Task<IReadOnlyCollection<ClienteBuscaResultado>> Handle(
        BuscarClientesQuery request,
        CancellationToken cancellationToken) =>
        repositorio.BuscarAsync(request.Pesquisa.Trim(), request.Limite, cancellationToken);
}

internal sealed class ObterClienteQueryHandler(IClientesRepositorio repositorio)
    : IRequestHandler<ObterClienteQuery, ClienteDetalheResultado>
{
    public async Task<ClienteDetalheResultado> Handle(
        ObterClienteQuery request,
        CancellationToken cancellationToken) =>
        await repositorio.ObterDetalheAsync(request.Id, cancellationToken)
        ?? throw new RecursoNaoEncontradoException("Cliente não encontrado.");
}

internal sealed class CriarClienteCommandHandler(
    IUsuarioContexto usuarioContexto,
    IClientesRepositorio repositorio)
    : IRequestHandler<CriarClienteCommand, ClienteDetalheResultado>
{
    public async Task<ClienteDetalheResultado> Handle(
        CriarClienteCommand request,
        CancellationToken cancellationToken)
    {
        var tipoPessoa = TipoPessoaParser.Converter(request.TipoPessoa);
        var documento = DocumentoFiscal.Normalizar(request.CpfCnpj);
        await ValidarDocumentoUnicoAsync(repositorio, documento, null, cancellationToken);
        var cliente = new Cliente(
            usuarioContexto.EmpresaId,
            request.Nome,
            tipoPessoa,
            documento,
            request.Telefone,
            request.WhatsApp,
            request.Email,
            request.DataNascimento,
            request.Observacao);
        repositorio.Adicionar(cliente);
        await repositorio.SalvarAsync(cancellationToken);
        return await repositorio.ObterDetalheAsync(cliente.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente não encontrado após o cadastro.");
    }

    internal static async Task ValidarDocumentoUnicoAsync(
        IClientesRepositorio repositorio,
        string? documento,
        Guid? ignorarClienteId,
        CancellationToken cancellationToken)
    {
        if (documento is not null &&
            await repositorio.DocumentoEmUsoAsync(documento, ignorarClienteId, cancellationToken))
        {
            throw new ConflitoRegraNegocioException(
                "Já existe um cliente com este CPF/CNPJ na empresa atual.");
        }
    }
}

internal sealed class AtualizarClienteCommandHandler(IClientesRepositorio repositorio)
    : IRequestHandler<AtualizarClienteCommand, ClienteDetalheResultado>
{
    public async Task<ClienteDetalheResultado> Handle(
        AtualizarClienteCommand request,
        CancellationToken cancellationToken)
    {
        var cliente = await repositorio.ObterParaAlteracaoAsync(request.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente não encontrado.");
        var tipoPessoa = TipoPessoaParser.Converter(request.TipoPessoa);
        var documento = DocumentoFiscal.Normalizar(request.CpfCnpj);
        await CriarClienteCommandHandler.ValidarDocumentoUnicoAsync(
            repositorio,
            documento,
            request.Id,
            cancellationToken);
        cliente.Atualizar(
            request.Nome,
            tipoPessoa,
            documento,
            request.Telefone,
            request.WhatsApp,
            request.Email,
            request.DataNascimento,
            request.Observacao);
        await repositorio.SalvarAsync(cancellationToken);
        return await repositorio.ObterDetalheAsync(cliente.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente não encontrado após a atualização.");
    }
}

internal sealed class AlterarStatusClienteCommandHandler(IClientesRepositorio repositorio)
    : IRequestHandler<AlterarStatusClienteCommand>
{
    public async Task Handle(AlterarStatusClienteCommand request, CancellationToken cancellationToken)
    {
        var cliente = await repositorio.ObterParaAlteracaoAsync(request.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Cliente não encontrado.");
        if (request.EhAtivo)
        {
            cliente.Ativar();
        }
        else
        {
            cliente.Desativar();
        }

        await repositorio.SalvarAsync(cancellationToken);
    }
}

internal static class TipoPessoaParser
{
    public static bool EhValido(string? valor) =>
        Enum.TryParse<TipoPessoa>(valor, true, out var tipo) && Enum.IsDefined(tipo);

    public static TipoPessoa Converter(string valor) =>
        Enum.TryParse<TipoPessoa>(valor, true, out var tipo) && Enum.IsDefined(tipo)
            ? tipo
            : throw new ArgumentException("O tipo de pessoa é inválido.", nameof(valor));
}
