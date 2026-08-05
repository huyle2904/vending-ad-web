using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Filters;

[AttributeUsage(AttributeTargets.Method)]
public class MobileRateLimitAttribute : Attribute, IAsyncActionFilter
{
    private readonly MobileRateLimitPolicy _policy;

    public MobileRateLimitAttribute(MobileRateLimitPolicy policy)
    {
        _policy = policy;
    }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var clientKey = ResolveClientKey(context);
        if (string.IsNullOrWhiteSpace(clientKey))
        {
            await next();
            return;
        }

        var rateLimiter = context.HttpContext.RequestServices.GetRequiredService<IMobileRateLimitService>();
        var timeService = context.HttpContext.RequestServices.GetRequiredService<ITimeService>();
        var result = rateLimiter.Check(_policy, clientKey, timeService.UtcNow);

        if (result.IsAllowed)
        {
            await next();
            return;
        }

        context.HttpContext.Response.Headers.RetryAfter = result.RetryAfterSeconds.ToString();
        context.Result = new ObjectResult(new
        {
            success = false,
            message = "Thiết bị gửi yêu cầu quá nhanh. Vui lòng thử lại sau.",
            retryAfterSeconds = result.RetryAfterSeconds
        })
        {
            StatusCode = StatusCodes.Status429TooManyRequests
        };
    }

    private string? ResolveClientKey(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("deviceCode", out var routeCode))
            return routeCode?.ToString();

        foreach (var argument in context.ActionArguments.Values)
        {
            var property = argument?.GetType().GetProperty("DeviceCode");
            var value = property?.GetValue(argument)?.ToString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        if (_policy == MobileRateLimitPolicy.Registration)
        {
            return context.HttpContext.Connection.RemoteIpAddress?.ToString()
                ?? "unknown-client";
        }

        return null;
    }
}
