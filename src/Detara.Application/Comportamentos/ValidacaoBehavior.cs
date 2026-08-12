using FluentValidation;
using MediatR;

namespace Detara.Application.Comportamentos;

internal sealed class ValidacaoBehavior<TRequest, TResponse>(IEnumerable<IValidator<TRequest>> validadores)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validadores.Any())
        {
            return await next(cancellationToken);
        }

        var contexto = new ValidationContext<TRequest>(request);
        var resultados = await Task.WhenAll(
            validadores.Select(x => x.ValidateAsync(contexto, cancellationToken)));
        var falhas = resultados.SelectMany(x => x.Errors).Where(x => x is not null).ToArray();

        if (falhas.Length > 0)
        {
            throw new ValidationException(falhas);
        }

        return await next(cancellationToken);
    }
}
