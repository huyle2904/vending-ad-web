using Microsoft.AspNetCore.Mvc;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Application.Services;

namespace VendingAdSystem.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
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
        return response.Success ? Ok(response) : Unauthorized(response);
    }

}
