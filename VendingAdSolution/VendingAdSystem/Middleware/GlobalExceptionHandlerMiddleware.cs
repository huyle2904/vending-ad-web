using System.Text.Encodings.Web;

namespace VendingAdSystem.Middleware;

public sealed class GlobalExceptionHandlerMiddleware
{
    private const string ErrorMessage = "Đã xảy ra lỗi. Vui lòng thử lại sau.";

    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionHandlerMiddleware> _logger;

    public GlobalExceptionHandlerMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionHandlerMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            var correlationId = ResolveCorrelationId(context);

            _logger.LogError(
                ex,
                "Unhandled exception for {Method} {Path}. CorrelationId: {CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                correlationId);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning(
                    "Cannot write error response because the response has already started. CorrelationId: {CorrelationId}",
                    correlationId);
                throw;
            }

            context.Response.Clear();
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.Headers[CorrelationIdMiddleware.CorrelationIdHeader] = correlationId;
            context.Response.Headers["Cache-Control"] = "no-store, no-cache";

            if (ShouldReturnJson(context.Request))
            {
                await WriteJsonResponseAsync(context, correlationId);
                return;
            }

            await WriteHtmlResponseAsync(context, correlationId);
        }
    }

    private static string ResolveCorrelationId(HttpContext context)
    {
        if (context.Items.TryGetValue(CorrelationIdMiddleware.CorrelationIdItemKey, out var itemValue) &&
            itemValue is string correlationId &&
            !string.IsNullOrWhiteSpace(correlationId))
        {
            return correlationId;
        }

        if (context.Request.Headers.TryGetValue(CorrelationIdMiddleware.CorrelationIdHeader, out var headerValue))
        {
            var incomingCorrelationId = headerValue.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(incomingCorrelationId))
                return incomingCorrelationId.Trim();
        }

        return context.TraceIdentifier;
    }

    private static bool ShouldReturnJson(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.IsNullOrWhiteSpace(request.ContentType) &&
            request.ContentType.Contains("json", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (request.Headers.TryGetValue("X-Requested-With", out var requestedWith) &&
            requestedWith.Any(value => string.Equals(value, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (request.Headers.TryGetValue("Accept", out var acceptValues) &&
            acceptValues.Any(value => value?.Contains("json", StringComparison.OrdinalIgnoreCase) == true))
        {
            return true;
        }

        return false;
    }

    private static Task WriteJsonResponseAsync(HttpContext context, string correlationId)
    {
        context.Response.ContentType = "application/json; charset=utf-8";

        return context.Response.WriteAsJsonAsync(new
        {
            success = false,
            message = ErrorMessage,
            correlationId
        });
    }

    private static Task WriteHtmlResponseAsync(HttpContext context, string correlationId)
    {
        context.Response.ContentType = "text/html; charset=utf-8";
        var encodedCorrelationId = HtmlEncoder.Default.Encode(correlationId);

        return context.Response.WriteAsync($$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1">
                <title>Unexpected error</title>
                <style>
                    body { font-family: Arial, sans-serif; background: #f5f7fb; color: #1f2937; margin: 0; }
                    main { max-width: 640px; margin: 10vh auto; background: #ffffff; padding: 32px; border-radius: 12px; box-shadow: 0 10px 30px rgba(15, 23, 42, 0.08); }
                    h1 { margin-top: 0; font-size: 1.75rem; }
                    p { line-height: 1.6; }
                    code { background: #f3f4f6; border-radius: 6px; padding: 2px 6px; }
                </style>
            </head>
            <body>
                <main>
                    <h1>Đã xảy ra lỗi</h1>
                    <p>{{ErrorMessage}}</p>
                    <p>Mã tra cứu: <code>{{encodedCorrelationId}}</code></p>
                </main>
            </body>
            </html>
            """);
    }
}
