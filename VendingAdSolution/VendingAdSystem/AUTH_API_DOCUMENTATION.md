# Authentication API Documentation

## Overview
The authentication system provides register and login endpoints for both User and Admin roles with JWT-like token generation and password hashing.

## API Endpoints

### User Authentication

#### Register User
- **POST** `api/auth/register/user`
- **Request Body:**
```json
{
  "email": "user@example.com",
  "password": "password123",
  "confirmPassword": "password123",
  "fullName": "John Doe"
}
```
- **Response (Success):**
```json
{
  "success": true,
  "message": "User registered successfully.",
  "token": null,
  "user": {
    "id": 1,
    "email": "user@example.com",
    "fullName": "John Doe",
    "role": "User"
  }
}
```

#### Login User
- **POST** `api/auth/login/user`
- **Request Body:**
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```
- **Response (Success):**
```json
{
  "success": true,
  "message": "Login successful.",
  "token": {
    "accessToken": "base64encodedtoken",
    "refreshToken": "base64refreshtoken",
    "expiresAt": "2024-05-08T12:34:56Z"
  },
  "user": {
    "id": 1,
    "email": "user@example.com",
    "fullName": "John Doe",
    "role": "User"
  }
}
```

### Admin Authentication

#### Register Admin
- **POST** `api/auth/register/admin`
- **Request Body:**
```json
{
  "email": "admin@example.com",
  "password": "adminpassword123",
  "confirmPassword": "adminpassword123",
  "fullName": "Admin User"
}
```
- **Response (Success):**
```json
{
  "success": true,
  "message": "Admin registered successfully.",
  "token": null,
  "user": {
    "id": 1,
    "email": "admin@example.com",
    "fullName": "Admin User",
    "role": "Admin"
  }
}
```

#### Login Admin
- **POST** `api/auth/login/admin`
- **Request Body:**
```json
{
  "email": "admin@example.com",
  "password": "adminpassword123"
}
```
- **Response (Success):**
```json
{
  "success": true,
  "message": "Login successful.",
  "token": {
    "accessToken": "base64encodedtoken",
    "refreshToken": "base64refreshtoken",
    "expiresAt": "2024-05-08T12:34:56Z"
  },
  "user": {
    "id": 1,
    "email": "admin@example.com",
    "fullName": "Admin User",
    "role": "Admin"
  }
}
```

## Features

✅ **Password Security**: Passwords are hashed with ASP.NET Core `PasswordHasher`; legacy SHA256 hashes are rehashed after successful login
✅ **Email Validation**: Ensures unique email addresses
✅ **Password Requirements**: Minimum 6 characters
✅ **Token Generation**: JWT-like access tokens with 24-hour expiration
✅ **Role-based**: Separate User and Admin models and authentication flows
✅ **Input Validation**: Request validation for all endpoints
✅ **Error Handling**: Clear error messages for validation failures

## Database Schema

### User Table
- Id (Primary Key)
- Email (Unique)
- PasswordHash
- FullName
- CreatedAt
- IsActive

### Admin Table
- Id (Primary Key)
- Email (Unique)
- PasswordHash
- FullName
- Role (Admin, SuperAdmin)
- CreatedAt
- IsActive

## Implementation Files

1. **Models/Models.cs** - User and Admin models
2. **Data/AppDbContext.cs** - Database context with User and Admin DbSets
3. **DTOs/AuthDtos.cs** - Data transfer objects for requests/responses
4. **Services/AuthService.cs** - Authentication business logic
5. **Controllers/AuthController.cs** - API endpoints
6. **Program.cs** - Dependency injection setup
