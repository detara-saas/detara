using FluentValidation;
using MediatR;

namespace Detara.Application.Plataforma;

public sealed record IniciarAutenticacaoPlataformaCommand(string Email, string Senha)
    : IRequest<InicioAutenticacaoPlataformaResultado>;

public sealed record ObterConfiguracaoMfaPlataformaQuery(string Desafio)
    : IRequest<ConfiguracaoMfaPlataformaResultado>;

public sealed record AtivarMfaPlataformaCommand(string Desafio, string Codigo, string? TraceId)
    : IRequest<SessaoPlataformaResultado>;

public sealed record VerificarMfaPlataformaCommand(string Desafio, string Codigo, string? TraceId)
    : IRequest<SessaoPlataformaResultado>;

public sealed record RegenerarCodigosRecuperacaoPlataformaCommand(
    string SenhaAtual,
    string CodigoTotp,
    string? TraceId)
    : IRequest<IReadOnlyCollection<string>>;

public sealed record SessaoPlataformaResultado(
    string Token,
    DateTime ExpiraEmUtc,
    Guid AdministradorId,
    string Nome,
    string Email,
    IReadOnlyCollection<string> CodigosRecuperacao);

internal sealed class IniciarAutenticacaoPlataformaHandler(IAutenticacaoPlataformaServico servico)
    : IRequestHandler<IniciarAutenticacaoPlataformaCommand, InicioAutenticacaoPlataformaResultado>
{
    public Task<InicioAutenticacaoPlataformaResultado> Handle(
        IniciarAutenticacaoPlataformaCommand request,
        CancellationToken cancellationToken) =>
        servico.IniciarAsync(request.Email, request.Senha, cancellationToken);
}

internal sealed class ObterConfiguracaoMfaPlataformaHandler(IAutenticacaoPlataformaServico servico)
    : IRequestHandler<ObterConfiguracaoMfaPlataformaQuery, ConfiguracaoMfaPlataformaResultado>
{
    public Task<ConfiguracaoMfaPlataformaResultado> Handle(
        ObterConfiguracaoMfaPlataformaQuery request,
        CancellationToken cancellationToken) =>
        servico.ObterConfiguracaoMfaAsync(request.Desafio, cancellationToken);
}

internal sealed class AtivarMfaPlataformaHandler(
    IAutenticacaoPlataformaServico servico,
    ITokenPlataformaServico tokenServico)
    : IRequestHandler<AtivarMfaPlataformaCommand, SessaoPlataformaResultado>
{
    public async Task<SessaoPlataformaResultado> Handle(
        AtivarMfaPlataformaCommand request,
        CancellationToken cancellationToken)
    {
        var resultado = await servico.AtivarMfaAsync(
            request.Desafio,
            request.Codigo,
            request.TraceId,
            cancellationToken);
        return CriarSessao(resultado, tokenServico.Gerar(resultado.Identidade));
    }

    internal static SessaoPlataformaResultado CriarSessao(
        AutenticacaoMfaPlataformaResultado resultado,
        TokenPlataformaGerado token) => new(
            token.Valor,
            token.ExpiraEmUtc,
            resultado.Identidade.Id,
            resultado.Identidade.Nome,
            resultado.Identidade.Email,
            resultado.CodigosRecuperacao);
}

internal sealed class VerificarMfaPlataformaHandler(
    IAutenticacaoPlataformaServico servico,
    ITokenPlataformaServico tokenServico)
    : IRequestHandler<VerificarMfaPlataformaCommand, SessaoPlataformaResultado>
{
    public async Task<SessaoPlataformaResultado> Handle(
        VerificarMfaPlataformaCommand request,
        CancellationToken cancellationToken)
    {
        var resultado = await servico.VerificarMfaAsync(
            request.Desafio,
            request.Codigo,
            request.TraceId,
            cancellationToken);
        return AtivarMfaPlataformaHandler.CriarSessao(
            resultado,
            tokenServico.Gerar(resultado.Identidade));
    }
}

internal sealed class RegenerarCodigosRecuperacaoPlataformaHandler(
    IAutenticacaoPlataformaServico servico,
    IContextoAdministradorPlataforma contexto)
    : IRequestHandler<RegenerarCodigosRecuperacaoPlataformaCommand, IReadOnlyCollection<string>>
{
    public Task<IReadOnlyCollection<string>> Handle(
        RegenerarCodigosRecuperacaoPlataformaCommand request,
        CancellationToken cancellationToken) =>
        servico.RegenerarCodigosRecuperacaoAsync(
            contexto.AdministradorPlataformaId,
            request.SenhaAtual,
            request.CodigoTotp,
            request.TraceId,
            cancellationToken);
}

internal sealed class IniciarAutenticacaoPlataformaValidator
    : AbstractValidator<IniciarAutenticacaoPlataformaCommand>
{
    public IniciarAutenticacaoPlataformaValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(200).EmailAddress();
        RuleFor(x => x.Senha).NotEmpty().MaximumLength(256);
    }
}

internal sealed class ObterConfiguracaoMfaPlataformaValidator
    : AbstractValidator<ObterConfiguracaoMfaPlataformaQuery>
{
    public ObterConfiguracaoMfaPlataformaValidator() =>
        RuleFor(x => x.Desafio).NotEmpty().MaximumLength(4000);
}

internal sealed class AtivarMfaPlataformaValidator : AbstractValidator<AtivarMfaPlataformaCommand>
{
    public AtivarMfaPlataformaValidator()
    {
        RuleFor(x => x.Desafio).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Codigo).Matches("^[0-9]{6}$").WithMessage("Informe o código de 6 dígitos.");
    }
}

internal sealed class VerificarMfaPlataformaValidator : AbstractValidator<VerificarMfaPlataformaCommand>
{
    public VerificarMfaPlataformaValidator()
    {
        RuleFor(x => x.Desafio).NotEmpty().MaximumLength(4000);
        RuleFor(x => x.Codigo).NotEmpty().MaximumLength(32);
    }
}

internal sealed class RegenerarCodigosRecuperacaoPlataformaValidator
    : AbstractValidator<RegenerarCodigosRecuperacaoPlataformaCommand>
{
    public RegenerarCodigosRecuperacaoPlataformaValidator()
    {
        RuleFor(x => x.SenhaAtual).NotEmpty().MaximumLength(256);
        RuleFor(x => x.CodigoTotp).Matches("^[0-9]{6}$").WithMessage("Informe o código de 6 dígitos.");
    }
}
