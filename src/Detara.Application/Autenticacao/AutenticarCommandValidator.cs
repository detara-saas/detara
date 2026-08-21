using FluentValidation;

namespace Detara.Application.Autenticacao;

internal sealed class AutenticarCommandValidator : AbstractValidator<AutenticarCommand>
{
    public AutenticarCommandValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Senha).NotEmpty().MaximumLength(200);
    }
}

internal sealed class SelecionarEmpresaCommandValidator : AbstractValidator<SelecionarEmpresaCommand>
{
    public SelecionarEmpresaCommandValidator()
    {
        RuleFor(x => x.Challenge).NotEmpty().MaximumLength(16000);
        RuleFor(x => x.EmpresaId).NotEmpty();
    }
}
