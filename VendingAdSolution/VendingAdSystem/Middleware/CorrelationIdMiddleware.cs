using Serilog.Context;

namespace VendingAdSystem.Middleware;

public sealed class CorrelationIdMiddleware
{
    public const string CorrelationIdHeader = "X-Correlation-ID";
    public const string CorrelationIdItemKey = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        context.TraceIdentifier = correlationId;
        context.Items[CorrelationIdItemKey] = correlationId;
        context.Response.Headers[CorrelationIdHeader] = correlationId;

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }

    private static string GetOrCreateCorrelationId(HttpContext context)
    {
        var incomingCorrelationId = context.Request.Headers[CorrelationIdHeader].FirstOrDefault();

        return string.IsNullOrWhiteSpace(incomingCorrelationId)
            ? Guid.NewGuid().ToString("N")
            : incomingCorrelationId.Trim();
    }
}
