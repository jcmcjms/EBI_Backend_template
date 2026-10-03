# Auth Backend Design Spec

## Overview

A .NET 10 modular monolith API with JWT authentication, custom user store (no Identity), and EF Core + SQL Server. Auth-only template serving as a starting point.

## Target Framework

- `net10.0`, C# `14.0`
- Nullable enabled, implicit usings enabled, warnings as errors

## Project Structure

```
src/EbiBackend/
├── EbiBackend.csproj
├── Program.cs                          # Minimal API host + middleware pipeline
├── appsettings.json                    # Non-secret config
├── appsettings.Development.json        # Dev overrides
├── Composition/
│   └── ServiceRegistration.cs          # Extension method: RegisterServices()
├── Features/
│   └── Auth/
│       ├── Contracts/
│       │   ├── RegisterRequest.cs      # record(string Email, string Password, string DisplayName)
│       │   ├── LoginRequest.cs         # record(string Email, string Password)
│       │   ├── TokenResponse.cs        # record(string AccessToken, string RefreshToken, DateTime ExpiresAt)
│       │   ├── RefreshRequest.cs       # record(string RefreshToken)
│       │   └── UserProfileResponse.cs  # record(Guid Id, string Email, string DisplayName)
│       ├── Endpoints/
│       │   └── AuthEndpoints.cs        # IEndpointRouteBuilder extension mapping all /auth/* routes
│       ├── Services/
│       │   ├── IAuthService.cs         # Interface for auth operations
│       │   ├── AuthService.cs          # Implementation: register, login, refresh, get profile
│       │   ├── ITokenService.cs        # Interface for JWT generation
│       │   └── TokenService.cs         # Implementation: access + refresh token generation
│       ├── Domain/
│       │   ├── User.cs                 # Entity: Id, Email, DisplayName, PasswordHash, CreatedAt
│       │   └── RefreshToken.cs         # Entity: Id, UserId, Token, ExpiresAt, CreatedAt, RevokedAt
│       ├── Persistence/
│       │   ├── AuthDbContext.cs         # DbContext with User + RefreshToken DbSets
│       │   ├── UserConfiguration.cs    # IEntityTypeConfiguration<User>
│       │   └── RefreshTokenConfiguration.cs # IEntityTypeConfiguration<RefreshToken>
│       └── Validation/
│           └── AuthValidator.cs        # Static validation methods for requests
├── Infrastructure/
│   ├── JwtSettings.cs                  # Options class: Issuer, Audience, SecretKey, AccessTokenExpiryMinutes, RefreshTokenExpiryDays
│   └── ExceptionHandlingMiddleware.cs  # Problem Details middleware
└── Shared/
    └── Result.cs                       # Result<T> pattern for service-layer outcomes

tests/EbiBackend.Tests/
├── EbiBackend.Tests.csproj
├── Features/
│   └── Auth/
│       ├── RegisterEndpointTests.cs
│       ├── LoginEndpointTests.cs
│       └── RefreshEndpointTests.cs
└── Helpers/
    └── TestWebApplicationFactory.cs    # WebApplicationFactory with in-memory DB for tests
```

Only the folders each feature needs are created. Empty folders are omitted.

## API Endpoints

All endpoints are Minimal API routes mapped in `AuthEndpoints.cs`.

### POST /auth/register

- **Input**: `RegisterRequest`
- **Validation**: Email format, password min length (8), display name required
- **Behavior**: Check email uniqueness → hash password → create User → return `UserProfileResponse`
- **Response**: `201 Created` with user profile; `409 Conflict` if email exists; `400 Bad Request` for validation failures

### POST /auth/login

- **Input**: `LoginRequest`
- **Behavior**: Find user by email → verify password hash → generate access + refresh tokens → persist refresh token → return `TokenResponse`
- **Response**: `200 OK` with tokens; `401 Unauthorized` for bad credentials

### POST /auth/refresh

- **Input**: `RefreshRequest`
- **Behavior**: Find refresh token in DB → validate not expired/revoked → rotate (revoke old, create new) → return new `TokenResponse`
- **Response**: `200 OK` with new tokens; `401 Unauthorized` for invalid/expired/revoked token

### GET /auth/me

- **Input**: JWT bearer token (Authorization header)
- **Behavior**: Extract user ID from claims → fetch user → return profile
- **Response**: `200 OK` with `UserProfileResponse`; `401 Unauthorized` if not authenticated; `404 Not Found` if user deleted

## Authentication Flow

```
Register → Login (get tokens) → Use access token for protected endpoints → Refresh when expired
```

- **Access token**: Short-lived (15 min default), contains `sub` (user ID), `email`, `jti`
- **Refresh token**: Long-lived (7 days default), stored in DB, single-use with rotation
- **Password hashing**: BCrypt with work factor 12

## Data Model

### User

| Column        | Type         | Constraints                     |
|---------------|-------------|---------------------------------|
| Id            | uniqueidentifier | PK, default NEWSEQUENTIALID() |
| Email         | nvarchar(256)    | NOT NULL, unique index        |
| DisplayName   | nvarchar(100)    | NOT NULL                      |
| PasswordHash  | nvarchar(512)    | NOT NULL                      |
| CreatedAt     | datetime2        | NOT NULL, default GETUTCDATE()|

### RefreshToken

| Column      | Type           | Constraints                          |
|-------------|---------------|--------------------------------------|
| Id          | uniqueidentifier | PK, default NEWSEQUENTIALID()      |
| UserId      | uniqueidentifier | FK → User(Id), NOT NULL            |
| Token       | nvarchar(512)    | NOT NULL, unique index             |
| ExpiresAt   | datetime2        | NOT NULL                           |
| CreatedAt   | datetime2        | NOT NULL, default GETUTCDATE()     |
| RevokedAt   | datetime2        | NULL (null = active)               |

Cascade delete: when User is deleted, their RefreshTokens are deleted.

## Configuration

JwtSettings bound from `appsettings.json` section `"Jwt"`:

```json
{
  "Jwt": {
    "Issuer": "EbiBackend",
    "Audience": "EbiBackend",
    "SecretKey": "<set via user secrets or env var>",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

Connection string in `"ConnectionStrings:DefaultConnection"`. Dev value in `appsettings.Development.json`; production via env vars.

## Dependency Injection (Composition/ServiceRegistration.cs)

```csharp
public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
{
    services.AddDbContext<AuthDbContext>(options =>
        options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

    services.AddScoped<IAuthService, AuthService>();
    services.AddScoped<ITokenService, TokenService>();

    services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

    // BCrypt, TimeProvider, etc.
    services.AddSingleton(TimeProvider.System);

    return services;
}
```

## Middleware Pipeline (Program.cs)

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterServices(builder.Configuration);
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(/* bind JwtSettings, validate issuer/audience/lifetime/signing key */);
builder.Services.AddAuthorization();

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

app.Run();
```

## Error Handling

`ExceptionHandlingMiddleware` catches unhandled exceptions and returns Problem Details (RFC 7807). Service-layer methods return `Result<T>` to avoid throwing for expected business failures (duplicate email, bad credentials).

## Validation

`AuthValidator` contains static methods:
- `ValidateRegisterRequest(RegisterRequest)` → `string[]` errors
- `ValidateLoginRequest(LoginRequest)` → `string[]` errors
- `ValidateRefreshRequest(RefreshRequest)` → `string[]` errors

Endpoints check validation before calling services, returning `400` with error details.

## Security Considerations

- SecretKey never logged or committed; configured via user secrets (dev) or env vars (prod)
- Refresh tokens are single-use with rotation to prevent replay
- BCrypt work factor 12 for password hashing
- Email uniqueness enforced at DB level
- CORS configured with explicit origins (not AllowAnyOrigin + credentials)
- No stack traces in error responses

## Dependencies (NuGet Packages)

- `Microsoft.EntityFrameworkCore.SqlServer` (EF Core 10)
- `Microsoft.EntityFrameworkCore.Design` (migrations)
- `BCrypt.Net-Next` (password hashing)
- `System.IdentityModel.Tokens.Jwt` (JWT creation)
- `Microsoft.AspNetCore.Authentication.JwtBearer` (JWT middleware)

Test project:
- `Microsoft.AspNetCore.Mvc.Testing` (WebApplicationFactory)
- `xunit` + `xunit.runner.visualstudio`
- `FluentAssertions`

## Testing Strategy

- **Integration tests** using `WebApplicationFactory` with an in-memory database
- Tests cover: register success/failure, login success/failure, refresh success/failure/expired/revoked, profile retrieval
- Each test is self-contained with DB seeding

## Out of Scope

- Email verification
- Password reset
- Role-based authorization (can be added later)
- Rate limiting
- Logging infrastructure (beyond what's built into ASP.NET)
- Docker/deployment configuration

## Open Decisions

None. All decisions resolved during brainstorming.