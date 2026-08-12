using FluentValidation;

namespace Detara.Application.Autenticacao;

internal sealed class AutenticarCommandValidator : AbstractValidator<AutenticarCommand>
{
    public AutenticarCommandValidator()
    {
        RuleFor(x => x.SlugEmpresa).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.Senha).NotEmpty().MaximumLength(200);
    }
}
