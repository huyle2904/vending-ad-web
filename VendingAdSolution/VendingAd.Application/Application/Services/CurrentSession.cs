using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace VendingAdSystem.Application.Services;

public interface ICurrentSession
{
    int? AdminId { get; }
    int? UserId { get; }
    string? AdminEmail { get; }
    string? UserEmail { get; }
    bool IsAdminLoggedIn { get; }
    bool IsPortalLoggedIn { get; }
}

public class CurrentSession : ICurrentSession
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentSession(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private ISession? Session => _httpContextAccessor.HttpContext?.Session;
    private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

    private bool HasRole(string role)
    {
        return User?.Identity?.IsAuthenticated == true && User.IsInRole(role);
    }

    private string? ClaimValue(string type)
    {
        return User?.FindFirstValue(type);
    }

    private static int? ParseId(string? value)
    {
        return int.TryParse(value, out var id) && id > 0 ? id : null;
    }

    public int? AdminId => Session?.GetInt32("AdminId")
        ?? (HasRole("Admin") ? ParseId(ClaimValue("AdminId") ?? ClaimValue(ClaimTypes.NameIdentifier)) : null);

    public int? UserId => Session?.GetInt32("UserId")
        ?? (HasRole("User") ? ParseId(ClaimValue("UserId") ?? ClaimValue(ClaimTypes.NameIdentifier)) : null);

    public string? AdminEmail => Session?.GetString("AdminEmail")
        ?? (HasRole("Admin") ? ClaimValue(ClaimTypes.Email) : null);

    public string? UserEmail => Session?.GetString("UserEmail")
        ?? (HasRole("User") ? ClaimValue(ClaimTypes.Email) : null);

    public bool IsAdminLoggedIn => AdminId.HasValue && AdminId.Value > 0 && !string.IsNullOrEmpty(AdminEmail);
    public bool IsPortalLoggedIn => UserId.HasValue && UserId.Value > 0 && !string.IsNullOrEmpty(UserEmail);
}
