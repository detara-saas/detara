using Detara.Contracts.Comum;
using Detara.Domain.Assinaturas;
using Detara.Infrastructure.Persistencia;
using Microsoft.EntityFrameworkCore;

namespace Detara.Api.Assinaturas;

public sealed class AssinaturaSuspensaMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, DetaraDbContext db)
    {
        if (context.User.Identity?.IsAuthenticated != true ||
            !Guid.TryParse(context.User.FindFirst("empresa_id")?.Value, out _) ||
            RotaPermitida(context.Request))
        {
            await next(context);
            return;
        }

        var suspensa = await db.AssinaturasEmpresas.AsNoTracking()
            .AnyAsync(x => x.Status == StatusAssinaturaEmpresa.Suspensa, context.RequestAborted);
        if (!suspensa)
        {
            await next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status402PaymentRequired;
        await context.Response.WriteAsJsonAsync(RespostaApi<object>.Falha(
            "O acesso operacional está temporariamente suspenso devido a uma pendência na assinatura.",
            "assinatura_suspensa"), context.RequestAborted);
    }

    private static bool RotaPermitida(HttpRequest request)
    {
        var caminho = request.Path;
        return caminho.StartsWithSegments("/api/autenticacao") ||
            caminho.StartsWithSegments("/api/assinatura") ||
            ((HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method)) &&
                (caminho.StartsWithSegments("/api/minha-conta") ||
                 caminho.StartsWithSegments("/api/empresa/logo")));
    }
}
