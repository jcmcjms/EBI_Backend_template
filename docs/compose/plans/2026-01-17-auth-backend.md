# Auth Backend Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use compose:subagent (recommended) or compose:execute to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a .NET 10 API with JWT authentication using a custom user store (no Identity), following the modular monolith architecture from the engineering rules.

**Architecture:** Single API project organized by feature under `Features/Auth/`. Thin Minimal API endpoints delegate to service-layer business logic. EF Core + SQL Server for persistence. JWT access tokens with refresh token rotation.

**Tech Stack:** .NET 10, C# 14, EF Core 10, SQL Server, BCrypt.Net-Next, System.IdentityModel.Tokens.Jwt, Minimal APIs, xUnit, WebApplicationFactory

## Global Constraints

- `net10.0` / C# `14.0` / nullable enabled / implicit usings / warnings as errors
- All I/O is async; propagate `CancellationToken` everywhere
- No `.Result`, `.Wait()`, `Thread.Sleep`, `Task.Run`, or `async void`
- No manual `HttpClient` instantiation
- Read-only EF queries use `AsNoTracking()`
- Problem Details for error responses; no stack traces leaked
- BCrypt work factor 12 for password hashing
- JWT secret configured via user secrets (dev) or env vars (prod); never committed
- Refresh tokens are single-use with rotation
- Email uniqueness enforced at database level

---

### Task 1: Solution and Project Scaffolding

**Covers:** Project setup

**Files:**
- Create: `src/EbiBackend/EbiBackend.csproj`
- Create: `src/EbiBackend/appsettings.json`
- Create: `src/EbiBackend/appsettings.Development.json`
- Create: `tests/EbiBackend.Tests/EbiBackend.Tests.csproj`

- [ ] **Step 1: Create solution and projects**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet new sln -n EbiBackend
dotnet new webapi -n EbiBackend -o src/EbiBackend --no-openapi
dotnet sln add src/EbiBackend/EbiBackend.csproj
```

- [ ] **Step 2: Configure EbiBackend.csproj for net10.0/C# 14 with warnings as errors**

Replace `src/EbiBackend/EbiBackend.csproj` content:

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14.0</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BCrypt.Net-Next" Version="4.0.3" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.0" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
    <PackageReference Include="System.IdentityModel.Tokens.Jwt" Version="8.0.0" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Restore packages**

```bash
cd src/EbiBackend
dotnet restore
```

- [ ] **Step 4: Create appsettings.json**

Write `src/EbiBackend/appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "DefaultConnection": "Server=localhost;Database=EbiBackend;Trusted_Connection=true;TrustServerCertificate=true"
  },
  "Jwt": {
    "Issuer": "EbiBackend",
    "Audience": "EbiBackend",
    "SecretKey": "SET_VIA_USER_SECRETS_OR_ENV_VAR",
    "AccessTokenExpiryMinutes": 15,
    "RefreshTokenExpiryDays": 7
  }
}
```

Write `src/EbiBackend/appsettings.Development.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

- [ ] **Step 5: Create test project**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet new xunit -n EbiBackend.Tests -o tests/EbiBackend.Tests
dotnet sln add tests/EbiBackend.Tests/EbiBackend.Tests.csproj
cd tests/EbiBackend.Tests
dotnet add reference ../../src/EbiBackend/EbiBackend.csproj
dotnet add package Microsoft.AspNetCore.Mvc.Testing --version 10.0.0
dotnet add package FluentAssertions --version 7.0.0
dotnet add package Microsoft.EntityFrameworkCore.InMemory --version 10.0.0
```

- [ ] **Step 6: Verify build**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build
```

Expected: Build succeeded with 0 warnings, 0 errors.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "feat: scaffold solution with API and test projects"
```

---

### Task 2: Domain Entities

**Covers:** User entity, RefreshToken entity

**Files:**
- Create: `src/EbiBackend/Features/Auth/Domain/User.cs`
- Create: `src/EbiBackend/Features/Auth/Domain/RefreshToken.cs`

- [ ] **Step 1: Create User entity**

Write `src/EbiBackend/Features/Auth/Domain/User.cs`:

```csharp
namespace EbiBackend.Features.Auth.Domain;

public sealed class User
{
    public Guid Id { get; set; }
    public required string Email { get; set; }
    public required string DisplayName { get; set; }
    public required string PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }

    public List<RefreshToken> RefreshTokens { get; set; } = [];
}
```

- [ ] **Step 2: Create RefreshToken entity**

Write `src/EbiBackend/Features/Auth/Domain/RefreshToken.cs`:

```csharp
namespace EbiBackend.Features.Auth.Domain;

public sealed class RefreshToken
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public required string Token { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt.HasValue;
    public bool IsActive => !IsRevoked && !IsExpired;

    public User User { get; set; } = null!;
}
```

- [ ] **Step 3: Verify build**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build src/EbiBackend
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/EbiBackend/Features/Auth/Domain/
git commit -m "feat: add User and RefreshToken domain entities"
```

---

### Task 3: EF Core Persistence

**Covers:** DbContext, entity configurations

**Files:**
- Create: `src/EbiBackend/Features/Auth/Persistence/AuthDbContext.cs`
- Create: `src/EbiBackend/Features/Auth/Persistence/UserConfiguration.cs`
- Create: `src/EbiBackend/Features/Auth/Persistence/RefreshTokenConfiguration.cs`

- [ ] **Step 1: Create UserConfiguration**

Write `src/EbiBackend/Features/Auth/Persistence/UserConfiguration.cs`:

```csharp
using EbiBackend.Features.Auth.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbiBackend.Features.Auth.Persistence;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(u => u.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.HasIndex(u => u.Email)
            .IsUnique();

        builder.Property(u => u.DisplayName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(u => u.PasswordHash)
            .HasMaxLength(512)
            .IsRequired();

        builder.Property(u => u.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.HasMany(u => u.RefreshTokens)
            .WithOne(rt => rt.User)
            .HasForeignKey(rt => rt.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 2: Create RefreshTokenConfiguration**

Write `src/EbiBackend/Features/Auth/Persistence/RefreshTokenConfiguration.cs`:

```csharp
using EbiBackend.Features.Auth.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EbiBackend.Features.Auth.Persistence;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.HasKey(rt => rt.Id);

        builder.Property(rt => rt.Id)
            .HasDefaultValueSql("NEWSEQUENTIALID()");

        builder.Property(rt => rt.Token)
            .HasMaxLength(512)
            .IsRequired();

        builder.HasIndex(rt => rt.Token)
            .IsUnique();

        builder.Property(rt => rt.ExpiresAt)
            .IsRequired();

        builder.Property(rt => rt.CreatedAt)
            .HasDefaultValueSql("GETUTCDATE()");

        builder.Ignore(rt => rt.IsExpired);
        builder.Ignore(rt => rt.IsRevoked);
        builder.Ignore(rt => rt.IsActive);
    }
}
```

- [ ] **Step 3: Create AuthDbContext**

Write `src/EbiBackend/Features/Auth/Persistence/AuthDbContext.cs`:

```csharp
using EbiBackend.Features.Auth.Domain;
using Microsoft.EntityFrameworkCore;

namespace EbiBackend.Features.Auth.Persistence;

public sealed class AuthDbContext : DbContext
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AuthDbContext).Assembly);
    }
}
```

- [ ] **Step 4: Verify build**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build src/EbiBackend
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/EbiBackend/Features/Auth/Persistence/
git commit -m "feat: add EF Core persistence layer for auth entities"
```

---

### Task 4: JWT Infrastructure

**Covers:** JwtSettings, TokenService

**Files:**
- Create: `src/EbiBackend/Infrastructure/JwtSettings.cs`
- Create: `src/EbiBackend/Features/Auth/Services/ITokenService.cs`
- Create: `src/EbiBackend/Features/Auth/Services/TokenService.cs`

- [ ] **Step 1: Create JwtSettings**

Write `src/EbiBackend/Infrastructure/JwtSettings.cs`:

```csharp
namespace EbiBackend.Infrastructure;

public sealed class JwtSettings
{
    public required string Issuer { get; set; }
    public required string Audience { get; set; }
    public required string SecretKey { get; set; }
    public int AccessTokenExpiryMinutes { get; set; } = 15;
    public int RefreshTokenExpiryDays { get; set; } = 7;
}
```

- [ ] **Step 2: Create ITokenService**

Write `src/EbiBackend/Features/Auth/Services/ITokenService.cs`:

```csharp
using EbiBackend.Features.Auth.Domain;

namespace EbiBackend.Features.Auth.Services;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
```

- [ ] **Step 3: Create TokenService**

Write `src/EbiBackend/Features/Auth/Services/TokenService.cs`:

```csharp
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using EbiBackend.Features.Auth.Domain;
using EbiBackend.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EbiBackend.Features.Auth.Services;

public sealed class TokenService : ITokenService
{
    private readonly JwtSettings _jwtSettings;

    public TokenService(IOptions<JwtSettings> jwtSettings)
    {
        _jwtSettings = jwtSettings.Value;
    }

    public string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.SecretKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _jwtSettings.Issuer,
            audience: _jwtSettings.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenExpiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }
}
```

- [ ] **Step 4: Verify build**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build src/EbiBackend
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/EbiBackend/Infrastructure/ src/EbiBackend/Features/Auth/Services/ITokenService.cs src/EbiBackend/Features/Auth/Services/TokenService.cs
git commit -m "feat: add JWT token generation infrastructure"
```

---

### Task 5: Auth Contracts and Validation

**Cores:** Request/response contracts, validation

**Files:**
- Create: `src/EbiBackend/Features/Auth/Contracts/RegisterRequest.cs`
- Create: `src/EbiBackend/Features/Auth/Contracts/LoginRequest.cs`
- Create: `src/EbiBackend/Features/Auth/Contracts/TokenResponse.cs`
- Create: `src/EbiBackend/Features/Auth/Contracts/RefreshRequest.cs`
- Create: `src/EbiBackend/Features/Auth/Contracts/UserProfileResponse.cs`
- Create: `src/EbiBackend/Features/Auth/Validation/AuthValidator.cs`
- Create: `src/EbiBackend/Shared/Result.cs`

- [ ] **Step 1: Create contracts**

Write `src/EbiBackend/Features/Auth/Contracts/RegisterRequest.cs`:

```csharp
namespace EbiBackend.Features.Auth.Contracts;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName);
```

Write `src/EbiBackend/Features/Auth/Contracts/LoginRequest.cs`:

```csharp
namespace EbiBackend.Features.Auth.Contracts;

public sealed record LoginRequest(
    string Email,
    string Password);
```

Write `src/EbiBackend/Features/Auth/Contracts/TokenResponse.cs`:

```csharp
namespace EbiBackend.Features.Auth.Contracts;

public sealed record TokenResponse(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);
```

Write `src/EbiBackend/Features/Auth/Contracts/RefreshRequest.cs`:

```csharp
namespace EbiBackend.Features.Auth.Contracts;

public sealed record RefreshRequest(
    string RefreshToken);
```

Write `src/EbiBackend/Features/Auth/Contracts/UserProfileResponse.cs`:

```csharp
namespace EbiBackend.Features.Auth.Contracts;

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    string DisplayName);
```

- [ ] **Step 2: Create Result<T>**

Write `src/EbiBackend/Shared/Result.cs`:

```csharp
namespace EbiBackend.Shared;

public sealed class Result<T>
{
    private Result(T value)
    {
        Value = value;
        IsSuccess = true;
        Error = null;
    }

    private Result(string error)
    {
        Value = default;
        IsSuccess = false;
        Error = error;
    }

    public T? Value { get; }
    public bool IsSuccess { get; }
    public string? Error { get; }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(string error) => new(error);
}
```

- [ ] **Step 3: Create AuthValidator**

Write `src/EbiBackend/Features/Auth/Validation/AuthValidator.cs`:

```csharp
using EbiBackend.Features.Auth.Contracts;

namespace EbiBackend.Features.Auth.Validation;

public static class AuthValidator
{
    public static string[] ValidateRegisterRequest(RegisterRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add("Email is required.");
        else if (!request.Email.Contains('@'))
            errors.Add("Email is not valid.");

        if (string.IsNullOrWhiteSpace(request.Password))
            errors.Add("Password is required.");
        else if (request.Password.Length < 8)
            errors.Add("Password must be at least 8 characters.");

        if (string.IsNullOrWhiteSpace(request.DisplayName))
            errors.Add("Display name is required.");

        return [.. errors];
    }

    public static string[] ValidateLoginRequest(LoginRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.Email))
            errors.Add("Email is required.");

        if (string.IsNullOrWhiteSpace(request.Password))
            errors.Add("Password is required.");

        return [.. errors];
    }

    public static string[] ValidateRefreshRequest(RefreshRequest request)
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            errors.Add("Refresh token is required.");

        return [.. errors];
    }
}
```

- [ ] **Step 4: Verify build**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build src/EbiBackend
```

Expected: Build succeeded.

- [ ] **Step 5: Commit**

```bash
git add src/EbiBackend/Features/Auth/Contracts/ src/EbiBackend/Features/Auth/Validation/ src/EbiBackend/Shared/
git commit -m "feat: add auth contracts, validation, and Result pattern"
```

---

### Task 6: Auth Service (Business Logic)

**Covers:** AuthService with register, login, refresh, profile operations

**Files:**
- Create: `src/EbiBackend/Features/Auth/Services/IAuthService.cs`
- Create: `src/EbiBackend/Features/Auth/Services/AuthService.cs`

- [ ] **Step 1: Create IAuthService**

Write `src/EbiBackend/Features/Auth/Services/IAuthService.cs`:

```csharp
using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Shared;

namespace EbiBackend.Features.Auth.Services;

public interface IAuthService
{
    Task<Result<UserProfileResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken = default);
    Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default);
}
```

- [ ] **Step 2: Create AuthService**

Write `src/EbiBackend/Features/Auth/Services/AuthService.cs`:

```csharp
using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Features.Auth.Domain;
using EbiBackend.Features.Auth.Persistence;
using EbiBackend.Shared;
using Microsoft.EntityFrameworkCore;

namespace EbiBackend.Features.Auth.Services;

public sealed class AuthService : IAuthService
{
    private readonly AuthDbContext _dbContext;
    private readonly ITokenService _tokenService;
    private readonly TimeProvider _timeProvider;

    public AuthService(AuthDbContext dbContext, ITokenService tokenService, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tokenService = tokenService;
        _timeProvider = timeProvider;
    }

    public async Task<Result<UserProfileResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var existingUser = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (existingUser is not null)
            return Result<UserProfileResponse>.Failure("A user with this email already exists.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = request.Email,
            DisplayName = request.DisplayName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password, workFactor: 12),
            CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<UserProfileResponse>.Success(new UserProfileResponse(user.Id, user.Email, user.DisplayName));
    }

    public async Task<Result<TokenResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email, cancellationToken);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Result<TokenResponse>.Failure("Invalid email or password.");

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var refreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = refreshToken,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now
        };

        _dbContext.RefreshTokens.Add(refreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<TokenResponse>.Success(new TokenResponse(
            accessToken,
            refreshToken,
            now.AddMinutes(15)));
    }

    public async Task<Result<TokenResponse>> RefreshAsync(RefreshRequest request, CancellationToken cancellationToken)
    {
        var refreshToken = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

        if (refreshToken is null || !refreshToken.IsActive)
            return Result<TokenResponse>.Failure("Invalid or expired refresh token.");

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == refreshToken.UserId, cancellationToken);

        if (user is null)
            return Result<TokenResponse>.Failure("User not found.");

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Revoke old token
        refreshToken.RevokedAt = now;

        // Generate new tokens
        var accessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        var newRefreshTokenEntity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now
        };

        _dbContext.RefreshTokens.Add(newRefreshTokenEntity);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result<TokenResponse>.Success(new TokenResponse(
            accessToken,
            newRefreshToken,
            now.AddMinutes(15)));
    }

    public async Task<Result<UserProfileResponse>> GetProfileAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
            return Result<UserProfileResponse>.Failure("User not found.");

        return Result<UserProfileResponse>.Success(new UserProfileResponse(user.Id, user.Email, user.DisplayName));
    }
}
```

- [ ] **Step 3: Verify build**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build src/EbiBackend
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/EbiBackend/Features/Auth/Services/IAuthService.cs src/EbiBackend/Features/Auth/Services/AuthService.cs
git commit -m "feat: add AuthService with register, login, refresh, profile operations"
```

---

### Task 7: Auth Endpoints

**Cores:** Minimal API endpoint mapping

**Files:**
- Create: `src/EbiBackend/Features/Auth/Endpoints/AuthEndpoints.cs`

- [ ] **Step 1: Create AuthEndpoints**

Write `src/EbiBackend/Features/Auth/Endpoints/AuthEndpoints.cs`:

```csharp
using System.Security.Claims;
using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Features.Auth.Services;
using EbiBackend.Features.Auth.Validation;
using Microsoft.AspNetCore.Mvc;

namespace EbiBackend.Features.Auth.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = AuthValidator.ValidateRegisterRequest(request);
            if (validationErrors.Length > 0)
                return Results.BadRequest(new { Errors = validationErrors });

            var result = await authService.RegisterAsync(request, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/auth/me", result.Value)
                : Results.Conflict(new { Error = result.Error });
        })
        .WithName("Register")
        .Produces<UserProfileResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = AuthValidator.ValidateLoginRequest(request);
            if (validationErrors.Length > 0)
                return Results.BadRequest(new { Errors = validationErrors });

            var result = await authService.LoginAsync(request, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .WithName("Login")
        .Produces<TokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", async (
            RefreshRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = AuthValidator.ValidateRefreshRequest(request);
            if (validationErrors.Length > 0)
                return Results.BadRequest(new { Errors = validationErrors });

            var result = await authService.RefreshAsync(request, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .WithName("Refresh")
        .Produces<TokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", async (
            ClaimsPrincipal user,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
                              ?? user.FindFirst("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Results.Unauthorized();

            var result = await authService.GetProfileAsync(userId, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { Error = result.Error });
        })
        .RequireAuthorization()
        .WithName("GetProfile")
        .Produces<UserProfileResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}
```

- [ ] **Step 2: Verify build**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build src/EbiBackend
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/EbiBackend/Features/Auth/Endpoints/
git commit -m "feat: add auth Minimal API endpoints"
```

---

### Task 8: Exception Handling Middleware

**Cores:** Problem Details error responses

**Files:**
- Create: `src/EbiBackend/Infrastructure/ExceptionHandlingMiddleware.cs`

- [ ] **Step 1: Create ExceptionHandlingMiddleware**

Write `src/EbiBackend/Infrastructure/ExceptionHandlingMiddleware.cs`:

```csharp
using Microsoft.AspNetCore.Mvc;

namespace EbiBackend.Infrastructure;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception occurred");

            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/problem+json";

            var problemDetails = new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "An unexpected error occurred.",
                Type = "https://tools.ietf.org/html/rfc7807"
            };

            await context.Response.WriteAsJsonAsync(problemDetails);
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build src/EbiBackend
```

Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/EbiBackend/Infrastructure/ExceptionHandlingMiddleware.cs
git commit -m "feat: add global exception handling middleware with Problem Details"
```

---

### Task 9: Composition and Program.cs Wiring

**Cores:** DI registration, middleware pipeline, JWT authentication setup

**Files:**
- Create: `src/EbiBackend/Composition/ServiceRegistration.cs`
- Modify: `src/EbiBackend/Program.cs`

- [ ] **Step 1: Create ServiceRegistration**

Write `src/EbiBackend/Composition/ServiceRegistration.cs`:

```csharp
using System.Text;
using EbiBackend.Features.Auth.Persistence;
using EbiBackend.Features.Auth.Services;
using EbiBackend.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace EbiBackend.Composition;

public static class ServiceRegistration
{
    public static IServiceCollection RegisterServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AuthDbContext>(options =>
            options.UseSqlServer(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ITokenService, TokenService>();

        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));

        services.AddSingleton(TimeProvider.System);

        var jwtSettings = configuration.GetSection("Jwt").Get<JwtSettings>()!;
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.SecretKey))
                };
            });

        services.AddAuthorization();

        return services;
    }
}
```

- [ ] **Step 2: Write Program.cs**

Replace `src/EbiBackend/Program.cs` content:

```csharp
using EbiBackend.Composition;
using EbiBackend.Features.Auth.Endpoints;
using EbiBackend.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.RegisterServices(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();

app.Run();

public partial class Program { }
```

- [ ] **Step 3: Verify build**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build
```

Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/EbiBackend/Composition/ src/EbiBackend/Program.cs
git commit -m "feat: wire up DI, authentication, and middleware pipeline"
```

---

### Task 10: Integration Tests

**Cores:** API tests for auth endpoints using WebApplicationFactory

**Files:**
- Create: `tests/EbiBackend.Tests/Helpers/TestWebApplicationFactory.cs`
- Create: `tests/EbiBackend.Tests/Features/Auth/RegisterEndpointTests.cs`
- Create: `tests/EbiBackend.Tests/Features/Auth/LoginEndpointTests.cs`
- Create: `tests/EbiBackend.Tests/Features/Auth/RefreshEndpointTests.cs`
- Modify: `tests/EbiBackend.Tests/UnitTest1.cs` (delete)

- [ ] **Step 1: Create TestWebApplicationFactory**

Write `tests/EbiBackend.Tests/Helpers/TestWebApplicationFactory.cs`:

```csharp
using EbiBackend.Features.Auth.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EbiBackend.Tests.Helpers;

public class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<AuthDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<AuthDbContext>(options =>
                options.UseInMemoryDatabase("TestDb"));
        });
    }
}
```

- [ ] **Step 2: Create RegisterEndpointTests**

Write `tests/EbiBackend.Tests/Features/Auth/RegisterEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using EbiBackend.Features.Auth.Contracts;
using FluentAssertions;

namespace EbiBackend.Tests.Features.Auth;

public class RegisterEndpointTests : IClassFixture<Helpers.TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RegisterEndpointTests(Helpers.TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreated()
    {
        var request = new RegisterRequest("test@example.com", "Password123!", "Test User");

        var response = await _client.PostAsJsonAsync("/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        user.Should().NotBeNull();
        user!.Email.Should().Be("test@example.com");
        user.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var request = new RegisterRequest("duplicate@example.com", "Password123!", "User One");

        await _client.PostAsJsonAsync("/auth/register", request);
        var response = await _client.PostAsJsonAsync("/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        var request = new RegisterRequest("invalid-email", "Password123!", "Test User");

        var response = await _client.PostAsJsonAsync("/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        var request = new RegisterRequest("test2@example.com", "short", "Test User");

        var response = await _client.PostAsJsonAsync("/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
```

- [ ] **Step 3: Create LoginEndpointTests**

Write `tests/EbiBackend.Tests/Features/Auth/LoginEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using EbiBackend.Features.Auth.Contracts;
using FluentAssertions;

namespace EbiBackend.Tests.Features.Auth;

public class LoginEndpointTests : IClassFixture<Helpers.TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LoginEndpointTests(Helpers.TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var registerRequest = new RegisterRequest("login@example.com", "Password123!", "Login User");
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest("login@example.com", "Password123!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokens.Should().NotBeNull();
        tokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var registerRequest = new RegisterRequest("wrong@example.com", "Password123!", "Wrong User");
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest("wrong@example.com", "WrongPassword!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ReturnsUnauthorized()
    {
        var loginRequest = new LoginRequest("noone@example.com", "Password123!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 4: Create RefreshEndpointTests**

Write `tests/EbiBackend.Tests/Features/Auth/RefreshEndpointTests.cs`:

```csharp
using System.Net;
using System.Net.Http.Json;
using EbiBackend.Features.Auth.Contracts;
using FluentAssertions;

namespace EbiBackend.Tests.Features.Auth;

public class RefreshEndpointTests : IClassFixture<Helpers.TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RefreshEndpointTests(Helpers.TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        var registerRequest = new RegisterRequest("refresh@example.com", "Password123!", "Refresh User");
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest("refresh@example.com", "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var refreshRequest = new RefreshRequest(tokens!.RefreshToken);
        var response = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        newTokens.Should().NotBeNull();
        newTokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
        newTokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        newTokens.RefreshToken.Should().NotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_ReturnsUnauthorized()
    {
        var registerRequest = new RegisterRequest("revoke@example.com", "Password123!", "Revoke User");
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest("revoke@example.com", "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var refreshRequest = new RefreshRequest(tokens!.RefreshToken);
        await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);

        var response = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        var refreshRequest = new RefreshRequest("invalid-token");
        var response = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
```

- [ ] **Step 5: Delete default test file**

Delete `tests/EbiBackend.Tests/UnitTest1.cs`.

- [ ] **Step 6: Run tests**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet test
```

Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add tests/EbiBackend.Tests/
git commit -m "test: add integration tests for auth endpoints"
```

---

## Final Verification

- [ ] **Build the entire solution**

```bash
cd "C:\Users\james\OneDrive\Desktop\Coding Buddy\Web Development\.Net Projects\EBI_Backend_template"
dotnet build
```

Expected: Build succeeded, 0 warnings, 0 errors.

- [ ] **Run all tests**

```bash
dotnet test
```

Expected: All tests pass.

- [ ] **Final commit**

```bash
git add -A
git commit -m "feat: complete auth backend template"
```