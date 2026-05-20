# Authentication Notes

This project has two authentication surfaces:

- MVC login for the CMS portal: `GET/POST /account/login`
- JSON API endpoints for user registration/login: `/api/auth/*`

Admin CMS login uses the MVC login page and falls back to admin authentication after user authentication fails. The current JSON `AuthController` exposes user endpoints only.

## MVC Login

Endpoint:

- `POST /account/login`

Form fields:

```text
username=<username-or-email>
password=<password>
returnUrl=<optional-local-url>
```

Behavior:

- Tries user login first.
- Falls back to admin login.
- Signs in with ASP.NET Core cookie authentication.
- Stores minimal session values used by existing controllers.
- Redirects users to `Portal/Dashboard`.
- Redirects admins to `Admin/Index`.

## JSON API

Register user:

```http
POST /api/auth/register/user
Content-Type: application/json
```

```json
{
  "username": "demo",
  "email": "demo@example.com",
  "password": "password123",
  "confirmPassword": "password123",
  "fullName": "Demo User"
}
```

Login user:

```http
POST /api/auth/login/user
Content-Type: application/json
```

```json
{
  "username": "demo@example.com",
  "password": "password123"
}
```

`LoginRequest.username` accepts either username or email. `LoginRequest.email` remains for backwards compatibility with older clients.

## Security Notes

- Passwords are hashed with ASP.NET Core `PasswordHasher`.
- Legacy SHA256 hashes are accepted only to allow rehash after a successful login.
- CMS authorization uses cookie roles (`Admin`, `User`) plus existing session helpers.
- Admin-created users still receive the temporary default password `TD@12345`; this is tracked in `REMAINING_REVIEW_ISSUES.md`.

## Implementation Files

- `VendingAd.Application/Application/DTOs/AuthDtos.cs`
- `VendingAd.Application/Application/Services/AuthService.cs`
- `VendingAdSystem/Controllers/AccountController.cs`
- `VendingAdSystem/Controllers/AuthController.cs`
- `VendingAd.Infrastructure/Infrastructure/Persistence/AppDbContext.cs`
