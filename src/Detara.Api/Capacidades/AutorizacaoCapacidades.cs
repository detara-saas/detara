using Detara.Application.Capacidades;
using Detara.Contracts.Comum;
using Detara.Domain.Capacidades;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;

namespace Detara.Api.Capacidades;

public static class PoliticasCapacidade
{
    public const string Veiculos = "Capacidade:veiculos";
    public const string CheckIn = "Capacidade:check-in";
}

public sealed record EmpresaCapacidadeRequirement(string Codigo) : IAuthorizationRequirement;

internal sealed class EmpresaCapacidadeAuthorizationHandler(IEmpresaCapacidadesServico capacidades)
    : AuthorizationHandler<EmpresaCapacidadeRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EmpresaCapacidadeRequirement requirement)
    {
        if (context.User.Identity?.IsAuthenticated == true &&
            await capacidades.PossuiAsync(requirement.Codigo))
        {
            context.Succeed(requirement);
        }
    }
}

internal sealed class CapacidadeAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _padrao = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        var capacidadeNegada = authorizeResult.Forbidden &&
            authorizeResult.AuthorizationFailure?.FailedRequirements
                .OfType<EmpresaCapacidadeRequirement>()
                .Any() == true;
        if (!capacidadeNegada)
        {
            await _padrao.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        await context.Response.WriteAsJsonAsync(RespostaApi<object>.Falha(
            "Esta funcionalidade não está habilitada para a empresa.",
            "capability_disabled"));
    }
}
