using Detara.Application.Abstracoes;
using Detara.Domain.Entidades;
using FluentValidation;
using MediatR;

namespace Detara.Application.Veiculos;

public sealed record FiltroVeiculos(
    int Pagina,
    int TamanhoPagina,
    string? Pesquisa,
    bool? EhAtivo,
    string Ordenacao = "veiculo");

public sealed record VeiculoListaItemResultado(
    Guid Id,
    string Descricao,
    string Placa,
    Guid ClienteId,
    string ClienteNome,
    int? AnoModelo,
    string? Cor,
    int? Quilometragem,
    bool EhAtivo);

public sealed record VeiculoDetalheResultado(
    Guid Id,
    Guid ClienteId,
    string ClienteNome,
    string Placa,
    string Marca,
    string Modelo,
    string? Versao,
    int? AnoFabricacao,
    int? AnoModelo,
    string? Cor,
    int? Quilometragem,
    string? Observacao,
    DateTime CriadoEmUtc,
    DateTime? AtualizadoEmUtc,
    bool EhAtivo);

public sealed record ListarVeiculosQuery(FiltroVeiculos Filtro)
    : IRequest<PaginacaoResultado<VeiculoListaItemResultado>>;

public sealed record ObterVeiculoQuery(Guid Id) : IRequest<VeiculoDetalheResultado>;

public sealed record CriarVeiculoCommand(
    Guid ClienteId,
    string Placa,
    string Marca,
    string Modelo,
    string? Versao,
    int? AnoFabricacao,
    int? AnoModelo,
    string? Cor,
    int? Quilometragem,
    string? Observacao) : IRequest<VeiculoDetalheResultado>;

public sealed record AtualizarVeiculoCommand(
    Guid Id,
    Guid ClienteId,
    string Placa,
    string Marca,
    string Modelo,
    string? Versao,
    int? AnoFabricacao,
    int? AnoModelo,
    string? Cor,
    int? Quilometragem,
    string? Observacao) : IRequest<VeiculoDetalheResultado>;

public sealed record AlterarStatusVeiculoCommand(Guid Id, bool EhAtivo) : IRequest;

internal sealed class ListarVeiculosQueryValidator : AbstractValidator<ListarVeiculosQuery>
{
    public ListarVeiculosQueryValidator()
    {
        RuleFor(item => item.Filtro.Pagina).GreaterThanOrEqualTo(1);
        RuleFor(item => item.Filtro.TamanhoPagina).Must(item => item is 10 or 25 or 50)
            .WithMessage("O tamanho da página deve ser 10, 25 ou 50.");
        RuleFor(item => item.Filtro.Pesquisa).MaximumLength(160);
        RuleFor(item => item.Filtro.Ordenacao).Must(item => item is "veiculo" or "criacao")
            .WithMessage("A ordenação deve ser por veículo ou criação.");
    }
}

internal sealed class CriarVeiculoCommandValidator : AbstractValidator<CriarVeiculoCommand>
{
    public CriarVeiculoCommandValidator()
    {
        RuleFor(item => item.ClienteId).NotEmpty();
        RuleFor(item => item.Placa).NotEmpty().MaximumLength(10);
        RuleFor(item => item.Marca).NotEmpty().MaximumLength(80);
        RuleFor(item => item.Modelo).NotEmpty().MaximumLength(80);
        RuleFor(item => item.Versao).MaximumLength(80);
        RuleFor(item => item.Cor).MaximumLength(50);
        RuleFor(item => item.Quilometragem).GreaterThanOrEqualTo(0).When(item => item.Quilometragem.HasValue);
        RuleFor(item => item.Observacao).MaximumLength(2000);
    }
}

internal sealed class AtualizarVeiculoCommandValidator : AbstractValidator<AtualizarVeiculoCommand>
{
    public AtualizarVeiculoCommandValidator()
    {
        RuleFor(item => item.Id).NotEmpty();
        RuleFor(item => item.ClienteId).NotEmpty();
        RuleFor(item => item.Placa).NotEmpty().MaximumLength(10);
        RuleFor(item => item.Marca).NotEmpty().MaximumLength(80);
        RuleFor(item => item.Modelo).NotEmpty().MaximumLength(80);
        RuleFor(item => item.Versao).MaximumLength(80);
        RuleFor(item => item.Cor).MaximumLength(50);
        RuleFor(item => item.Quilometragem).GreaterThanOrEqualTo(0).When(item => item.Quilometragem.HasValue);
        RuleFor(item => item.Observacao).MaximumLength(2000);
    }
}

internal sealed class AlterarStatusVeiculoCommandValidator : AbstractValidator<AlterarStatusVeiculoCommand>
{
    public AlterarStatusVeiculoCommandValidator() => RuleFor(item => item.Id).NotEmpty();
}

internal sealed class ListarVeiculosQueryHandler(IVeiculosRepositorio repositorio)
    : IRequestHandler<ListarVeiculosQuery, PaginacaoResultado<VeiculoListaItemResultado>>
{
    public Task<PaginacaoResultado<VeiculoListaItemResultado>> Handle(
        ListarVeiculosQuery request,
        CancellationToken cancellationToken) =>
        repositorio.ListarAsync(request.Filtro, cancellationToken);
}

internal sealed class ObterVeiculoQueryHandler(IVeiculosRepositorio repositorio)
    : IRequestHandler<ObterVeiculoQuery, VeiculoDetalheResultado>
{
    public async Task<VeiculoDetalheResultado> Handle(
        ObterVeiculoQuery request,
        CancellationToken cancellationToken) =>
        await repositorio.ObterDetalheAsync(request.Id, cancellationToken)
        ?? throw new RecursoNaoEncontradoException("Veículo não encontrado.");
}

internal sealed class CriarVeiculoCommandHandler(
    IUsuarioContexto usuarioContexto,
    IClientesRepositorio clientesRepositorio,
    IVeiculosRepositorio veiculosRepositorio)
    : IRequestHandler<CriarVeiculoCommand, VeiculoDetalheResultado>
{
    public async Task<VeiculoDetalheResultado> Handle(
        CriarVeiculoCommand request,
        CancellationToken cancellationToken)
    {
        await ValidarClienteAsync(
            clientesRepositorio,
            request.ClienteId,
            usuarioContexto.EmpresaId,
            cancellationToken);
        var placa = Veiculo.NormalizarPlaca(request.Placa);
        await ValidarPlacaUnicaAsync(veiculosRepositorio, placa, null, cancellationToken);
        var veiculo = new Veiculo(
            usuarioContexto.EmpresaId,
            request.ClienteId,
            placa,
            request.Marca,
            request.Modelo,
            request.Versao,
            request.AnoFabricacao,
            request.AnoModelo,
            request.Cor,
            request.Quilometragem,
            request.Observacao);
        veiculosRepositorio.Adicionar(veiculo);
        await veiculosRepositorio.SalvarAsync(cancellationToken);
        return await veiculosRepositorio.ObterDetalheAsync(veiculo.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Veículo não encontrado após o cadastro.");
    }

    internal static async Task ValidarClienteAsync(
        IClientesRepositorio repositorio,
        Guid clienteId,
        Guid empresaId,
        CancellationToken cancellationToken)
    {
        if (!await repositorio.PertenceAoTenantEAtivoAsync(clienteId, empresaId, cancellationToken))
        {
            throw new RecursoNaoEncontradoException(
                "Cliente não encontrado, inativo ou fora da empresa atual.");
        }
    }

    internal static async Task ValidarPlacaUnicaAsync(
        IVeiculosRepositorio repositorio,
        string placa,
        Guid? ignorarVeiculoId,
        CancellationToken cancellationToken)
    {
        if (await repositorio.PlacaEmUsoAsync(placa, ignorarVeiculoId, cancellationToken))
        {
            throw new ConflitoRegraNegocioException(
                "Já existe um veículo com esta placa na empresa atual.");
        }
    }
}

internal sealed class AtualizarVeiculoCommandHandler(
    IUsuarioContexto usuarioContexto,
    IClientesRepositorio clientesRepositorio,
    IVeiculosRepositorio veiculosRepositorio)
    : IRequestHandler<AtualizarVeiculoCommand, VeiculoDetalheResultado>
{
    public async Task<VeiculoDetalheResultado> Handle(
        AtualizarVeiculoCommand request,
        CancellationToken cancellationToken)
    {
        var veiculo = await veiculosRepositorio.ObterParaAlteracaoAsync(request.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Veículo não encontrado.");
        await CriarVeiculoCommandHandler.ValidarClienteAsync(
            clientesRepositorio,
            request.ClienteId,
            usuarioContexto.EmpresaId,
            cancellationToken);
        var placa = Veiculo.NormalizarPlaca(request.Placa);
        await CriarVeiculoCommandHandler.ValidarPlacaUnicaAsync(
            veiculosRepositorio,
            placa,
            request.Id,
            cancellationToken);
        veiculo.Atualizar(
            request.ClienteId,
            placa,
            request.Marca,
            request.Modelo,
            request.Versao,
            request.AnoFabricacao,
            request.AnoModelo,
            request.Cor,
            request.Quilometragem,
            request.Observacao);
        await veiculosRepositorio.SalvarAsync(cancellationToken);
        return await veiculosRepositorio.ObterDetalheAsync(veiculo.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Veículo não encontrado após a atualização.");
    }
}

internal sealed class AlterarStatusVeiculoCommandHandler(IVeiculosRepositorio repositorio)
    : IRequestHandler<AlterarStatusVeiculoCommand>
{
    public async Task Handle(AlterarStatusVeiculoCommand request, CancellationToken cancellationToken)
    {
        var veiculo = await repositorio.ObterParaAlteracaoAsync(request.Id, cancellationToken)
            ?? throw new RecursoNaoEncontradoException("Veículo não encontrado.");
        if (request.EhAtivo)
        {
            veiculo.Ativar();
        }
        else
        {
            veiculo.Desativar();
        }

        await repositorio.SalvarAsync(cancellationToken);
    }
}
