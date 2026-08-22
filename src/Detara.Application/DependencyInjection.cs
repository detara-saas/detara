using Detara.Application.Comportamentos;
using Detara.Application.Agenda;
using Detara.Application.Financeiro;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Detara.Application.Notificacoes;

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
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IIntegracaoFinanceiroOrdensServico, IntegracaoFinanceiroOrdensServico>();
        services.AddScoped<IIntegracaoNotificacoesOrdensServico, IntegracaoNotificacoesOrdensServico>();

        return services;
    }
}
