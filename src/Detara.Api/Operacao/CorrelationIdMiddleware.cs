using System.Diagnostics;

namespace Detara.Api.Operacao;

internal sealed class CorrelationIdMiddleware(
    RequestDelegate next,
    ILogger<CorrelationIdMiddleware> logger)
{
    public const string Header = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = ObterCorrelationId(context.Request.Headers[Header].ToString());
        context.TraceIdentifier = correlationId;
        context.Response.Headers[Header] = correlationId;

        using var scope = logger.BeginScope(new Dictionary<string, object>
        {
            ["CorrelationId"] = correlationId
        });
        var inicio = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            var duracaoMs = Stopwatch.GetElapsedTime(inicio).TotalMilliseconds;
            logger.LogInformation(
                "HTTP {Method} {Path} respondeu {StatusCode} em {DurationMs:F1} ms",
                context.Request.Method,
                context.Request.Path.Value ?? "/",
                context.Response.StatusCode,
                duracaoMs);
        }
    }

    private static string ObterCorrelationId(string? recebido) =>
        Guid.TryParse(recebido, out var correlationId)
            ? correlationId.ToString("N")
            : Guid.NewGuid().ToString("N");
}
