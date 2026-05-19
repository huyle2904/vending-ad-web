using Microsoft.AspNetCore.Http;

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

    public int? AdminId => Session?.GetInt32("AdminId");
    public int? UserId => Session?.GetInt32("UserId");
    public string? AdminEmail => Session?.GetString("AdminEmail");
    public string? UserEmail => Session?.GetString("UserEmail");
    public bool IsAdminLoggedIn => AdminId.HasValue && AdminId.Value > 0 && !string.IsNullOrEmpty(AdminEmail);
    public bool IsPortalLoggedIn => UserId.HasValue && UserId.Value > 0 && !string.IsNullOrEmpty(UserEmail);
}
