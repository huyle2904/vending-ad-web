using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using VendingAdSystem.Application.DTOs;
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
        var deviceCode = ResolveDeviceCode(context);
        if (string.IsNullOrWhiteSpace(deviceCode))
        {
            await next();
            return;
        }

        var rateLimiter = context.HttpContext.RequestServices.GetRequiredService<IMobileRateLimitService>();
        var timeService = context.HttpContext.RequestServices.GetRequiredService<ITimeService>();
        var result = rateLimiter.Check(_policy, deviceCode, timeService.UtcNow);

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

    private static string? ResolveDeviceCode(ActionExecutingContext context)
    {
        if (context.ActionArguments.TryGetValue("deviceCode", out var routeCode))
            return routeCode?.ToString();

        if (context.ActionArguments.TryGetValue("request", out var request) && request is MobileHeartbeatRequest heartbeatRequest)
            return heartbeatRequest.DeviceCode;

        return null;
    }
}
