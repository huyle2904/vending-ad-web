using Microsoft.AspNetCore.Mvc;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IAuditService _auditService;

    public AuthController(IAuthService authService, IAuditService auditService)
    {
        _authService = authService;
        _auditService = auditService;
    }

    [HttpPost("register/user")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _authService.RegisterUserAsync(request);
        return response.Success ? Ok(response) : BadRequest(response);
    }

    [HttpPost("login/user")]
    public async Task<IActionResult> LoginUser([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var response = await _authService.LoginUserAsync(request);
        if (response.Success && response.User != null)
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                ActorType = AuditActorTypes.User,
                ActorId = response.User.Id,
                Action = AuditActions.Login,
                TargetType = AuditTargets.User,
                TargetId = response.User.Id,
                Details = new
                {
                    Login = ResolveLoginIdentifier(request),
                    Channel = "Api"
                }
            });
        }
        else
        {
            await _auditService.LogAsync(new AuditLogEntry
            {
                ActorType = AuditActorTypes.Anonymous,
                Action = AuditActions.LoginFailed,
                TargetType = AuditTargets.Account,
                Details = new
                {
                    Login = ResolveLoginIdentifier(request),
                    Channel = "Api"
                }
            });
        }

        return response.Success ? Ok(response) : Unauthorized(response);
    }

    private static string ResolveLoginIdentifier(LoginRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.Username)
            ? request.Username.Trim()
            : request.Email.Trim();
    }

}
