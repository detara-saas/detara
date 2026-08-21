using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Detara.Api.Operacao;

internal static class RespostaHealthCheck
{
    public static Task EscreverAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new
        {
            status = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy"
        });
    }
}
