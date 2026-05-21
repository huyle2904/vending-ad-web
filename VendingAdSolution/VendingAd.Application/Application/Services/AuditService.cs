using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IAuditService
{
    Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default);
}

public sealed class AuditLogEntry
{
    public string? ActorType { get; init; }
    public int? ActorId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string TargetType { get; init; } = string.Empty;
    public int? TargetId { get; init; }
    public object? Details { get; init; }
}

public static class AuditActorTypes
{
    public const string Admin = "Admin";
    public const string User = "User";
    public const string Device = "Device";
    public const string Anonymous = "Anonymous";
}

public static class AuditTargets
{
    public const string Account = "Account";
    public const string Admin = "Admin";
    public const string User = "User";
    public const string Device = "Device";
    public const string Media = "Media";
    public const string MediaBatch = "MediaBatch";
    public const string PlaybackSchedule = "PlaybackSchedule";
}

public static class AuditActions
{
    public const string Login = "Login";
    public const string LoginFailed = "LoginFailed";
    public const string Logout = "Logout";
    public const string CreateUser = "CreateUser";
    public const string UpdateUser = "UpdateUser";
    public const string ToggleUserActive = "ToggleUserActive";
    public const string ResetPassword = "ResetPassword";
    public const string RotateDeviceSecret = "RotateDeviceSecret";
    public const string RevokeDeviceSecret = "RevokeDeviceSecret";
    public const string UploadVideo = "UploadVideo";
    public const string DeleteVideo = "DeleteVideo";
    public const string CreateSchedule = "CreateSchedule";
    public const string UpdateSchedule = "UpdateSchedule";
    public const string DeleteSchedule = "DeleteSchedule";
    public const string ToggleSchedule = "ToggleSchedule";
    public const string AddScheduleItem = "AddScheduleItem";
    public const string RemoveScheduleItem = "RemoveScheduleItem";
    public const string ReorderScheduleItems = "ReorderScheduleItems";
}

public sealed class AuditService : IAuditService
{
    private const int ActorTypeMaxLength = 32;
    private const int ActionMaxLength = 128;
    private const int TargetTypeMaxLength = 64;
    private const int CorrelationIdMaxLength = 128;
    private const int IpAddressMaxLength = 64;
    private const int UserAgentMaxLength = 512;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IRepository<AuditLog> _auditLogs;
    private readonly ICurrentSession _currentSession;
    private readonly ITimeService _timeService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<AuditService> _logger;

    public AuditService(
        IRepository<AuditLog> auditLogs,
        ICurrentSession currentSession,
        ITimeService timeService,
        IHttpContextAccessor httpContextAccessor,
        ILogger<AuditService> logger)
    {
        _auditLogs = auditLogs;
        _currentSession = currentSession;
        _timeService = timeService;
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public async Task LogAsync(AuditLogEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrWhiteSpace(entry.Action))
            throw new ArgumentException("Audit action is required.", nameof(entry));

        if (string.IsNullOrWhiteSpace(entry.TargetType))
            throw new ArgumentException("Audit target type is required.", nameof(entry));

        var httpContext = _httpContextAccessor.HttpContext;
        var actorType = ResolveActorType(entry.ActorType);
        var actorId = ResolveActorId(actorType, entry.ActorId);

        var auditLog = new AuditLog
        {
            Timestamp = _timeService.UtcNow,
            ActorType = TrimRequiredToLength(actorType, ActorTypeMaxLength),
            ActorId = actorId,
            Action = TrimRequiredToLength(entry.Action.Trim(), ActionMaxLength),
            TargetType = TrimRequiredToLength(entry.TargetType.Trim(), TargetTypeMaxLength),
            TargetId = entry.TargetId,
            CorrelationId = TrimToLength(httpContext?.TraceIdentifier, CorrelationIdMaxLength),
            IpAddress = TrimToLength(httpContext?.Connection.RemoteIpAddress?.ToString(), IpAddressMaxLength),
            UserAgent = TrimToLength(httpContext?.Request.Headers.UserAgent.ToString(), UserAgentMaxLength),
            Details = SerializeDetails(entry.Details, httpContext)
        };

        try
        {
            await _auditLogs.AddAsync(auditLog);
            await _auditLogs.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to persist audit log for action {Action} on {TargetType} {TargetId}",
                auditLog.Action,
                auditLog.TargetType,
                auditLog.TargetId);
        }
    }

    private string ResolveActorType(string? actorType)
    {
        if (!string.IsNullOrWhiteSpace(actorType))
            return actorType.Trim();

        if (_currentSession.AdminId.HasValue)
            return AuditActorTypes.Admin;

        if (_currentSession.UserId.HasValue)
            return AuditActorTypes.User;

        return AuditActorTypes.Anonymous;
    }

    private int? ResolveActorId(string actorType, int? actorId)
    {
        if (actorId.HasValue)
            return actorId;

        if (string.Equals(actorType, AuditActorTypes.Admin, StringComparison.OrdinalIgnoreCase))
            return _currentSession.AdminId;

        if (string.Equals(actorType, AuditActorTypes.User, StringComparison.OrdinalIgnoreCase))
            return _currentSession.UserId;

        return null;
    }

    private static string? SerializeDetails(object? details, HttpContext? httpContext)
    {
        if (details == null && httpContext == null)
            return null;

        var payload = new
        {
            Method = httpContext?.Request.Method,
            Path = httpContext?.Request.Path.Value,
            QueryString = httpContext?.Request.QueryString.Value,
            Data = details
        };

        return JsonSerializer.Serialize(payload, JsonOptions);
    }

    private static string? TrimToLength(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }

    private static string TrimRequiredToLength(string value, int maxLength)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
