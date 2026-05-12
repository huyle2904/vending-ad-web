using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VendingAdSystem.Application.DTOs;
using VendingAdSystem.Domain.Entities;
using VendingAdSystem.Infrastructure.Repositories.Interfaces;

namespace VendingAdSystem.Application.Services;

public interface IAuthService
{
    Task<AuthResponse> RegisterUserAsync(RegisterRequest request);
    Task<AuthResponse> LoginUserAsync(LoginRequest request);
    Task<AuthResponse> LoginAdminAsync(LoginRequest request);
}

public class AuthService : IAuthService
{
    private readonly IRepository<User> _users;
    private readonly IRepository<Admin> _admins;

    public AuthService(IRepository<User> users, IRepository<Admin> admins)
    {
        _users = users;
        _admins = admins;
    }

    public async Task<AuthResponse> RegisterUserAsync(RegisterRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return new AuthResponse { Success = false, Message = "Tên đăng nhập và mật khẩu là bắt buộc." };

        if (request.Password != request.ConfirmPassword)
            return new AuthResponse { Success = false, Message = "Passwords do not match." };

        if (request.Password.Length < 6)
            return new AuthResponse { Success = false, Message = "Password must be at least 6 characters." };

        var username = request.Username.Trim();
        var existingUser = await _users.Query().FirstOrDefaultAsync(u => u.Username == username);
        if (existingUser != null)
            return new AuthResponse { Success = false, Message = "Username already exists." };

        var user = new User
        {
            Username = username,
            Email = request.Email,
            PasswordHash = HashPassword(request.Password),
            FullName = request.FullName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        await _users.AddAsync(user);
        await _users.SaveChangesAsync();

        return new AuthResponse
        {
            Success = true,
            Message = "Đăng ký người dùng thành công.",
            User = ToUserInfo(user)
        };
    }

    public async Task<AuthResponse> LoginUserAsync(LoginRequest request)
    {
        var login = GetLoginValue(request);
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(request.Password))
            return new AuthResponse { Success = false, Message = "Tên đăng nhập và mật khẩu là bắt buộc." };

        var user = await _users.Query().FirstOrDefaultAsync(u => u.Username == login || u.Email == login);
        if (user == null || !VerifyPassword(request.Password, user.PasswordHash) || !user.IsActive)
            return new AuthResponse { Success = false, Message = "Invalid email or password." };

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful.",
            Token = GenerateToken(user.Id.ToString(), user.Email, "User"),
            User = ToUserInfo(user)
        };
    }

    public async Task<AuthResponse> LoginAdminAsync(LoginRequest request)
    {
        var login = GetLoginValue(request);
        if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(request.Password))
            return new AuthResponse { Success = false, Message = "Tên đăng nhập và mật khẩu là bắt buộc." };

        var admin = await _admins.Query().FirstOrDefaultAsync(a => a.Email == login);
        if (admin == null || !VerifyPassword(request.Password, admin.PasswordHash) || !admin.IsActive)
            return new AuthResponse { Success = false, Message = "Invalid email or password." };

        return new AuthResponse
        {
            Success = true,
            Message = "Login successful.",
            Token = GenerateToken(admin.Id.ToString(), admin.Email, admin.Role),
            User = new UserInfo
            {
                Id = admin.Id,
                Username = admin.Email,
                Email = admin.Email,
                FullName = admin.FullName,
                Role = admin.Role
            }
        };
    }

    private static UserInfo ToUserInfo(User user)
    {
        return new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            FullName = user.FullName,
            Role = "User"
        };
    }

    private static string HashPassword(string password)
    {
        using var sha256 = SHA256.Create();
        var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
        return Convert.ToBase64String(hashedBytes);
    }

    private static bool VerifyPassword(string password, string hash)
    {
        return HashPassword(password) == hash;
    }

    private static string GetLoginValue(LoginRequest request)
    {
        return !string.IsNullOrWhiteSpace(request.Username)
            ? request.Username.Trim()
            : request.Email.Trim();
    }

    private static AuthToken GenerateToken(string userId, string email, string role)
    {
        var expiresAt = DateTime.UtcNow.AddHours(24);
        var accessToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userId}|{email}|{role}|{expiresAt.Ticks}"));

        return new AuthToken
        {
            AccessToken = accessToken,
            RefreshToken = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{userId}|{Guid.NewGuid()}")),
            ExpiresAt = expiresAt
        };
    }
}
