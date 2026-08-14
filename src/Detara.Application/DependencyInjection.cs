using Detara.Application.Comportamentos;
using Detara.Application.Agenda;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Detara.Application;

public static class DependencyInjection
{
    public static IServiceCollection AdicionarApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(configuracao => configuracao.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly, includeInternalTypes: true);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidacaoBehavior<,>));
        services.AddSingleton<IConversorFusoHorario, ConversorFusoHorario>();

        return services;
    }
}
