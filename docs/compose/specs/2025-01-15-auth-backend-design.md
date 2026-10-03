# Authentication Backend Design Spec

**Date:** 2025-01-15
**Project:** EBI Backend Template
**Status:** Approved for implementation

---

## 1. Overview

Build a modular monolith backend with custom authentication (no ASP.NET Core Identity) using:
- JWT tokens (HS256 symmetric signing) for stateless auth
- Argon2id password hashing via `Konscious.Security.Cryptography.Argon2`
- SQL Server with EF Core 10
- Refresh token rotation with secure storage
- Role-based authorization (Admin, User)
- OpenAPI documentation with Scalar UI

---

## 2. Architecture

### 2.1 Project Structure

```
src/Ebi.Api/
├── Composition/                 # DI registration, middleware pipeline
├── Features/
│   ├── Auth/
│   │   ├── Contracts/           # Request/Response DTOs
│   │   ├── Endpoints/           # Minimal API endpoints
│   │   ├── Services/            # Auth use cases (register, login, refresh, logout)
│   │   ├── Domain/              # User, Role, RefreshToken entities + password hashing
│   │   ├── Persistence/         # EF Core config, DbContext, repositories
│   │   └── Validation/          # FluentValidation rules
│   └── Users/                   # Future: user profile management
├── Infrastructure/
│   ├── Database/                # EF Core DbContext, migrations
│   ├── Security/                # JWT token service, Argon2 hasher
│   └── Time/                    # TimeProvider abstraction
├── Shared/                      # Cross-cutting: ProblemDetails, extensions
└── Program.cs                   # App entry point
```

### 2.2 Dependency Direction

```
HTTP Endpoint (Auth.Endpoints)
    ↓
Feature Service (Auth.Services)
    ↓
Domain + Persistence (Auth.Domain, Auth.Persistence)
    ↓
Infrastructure (Infrastructure.Security, Infrastructure.Database)
```

---

## 3. Data Model

### 3.1 User Entity

```csharp
public sealed class User
{
    public Guid Id { get; private set; }
    public string Email { get; private set; }          // unique, normalized lower
    public string PasswordHash { get; private set; }   // Argon2id hash
    public string FullName { get; private set; }
    public bool IsEmailVerified { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    
    // Navigation
    public ICollection<UserRole> UserRoles { get; private set; } = [];
    public ICollection<RefreshToken> RefreshTokens { get; private set; } = [];
}
```

### 3.2 Role Entity

```csharp
public sealed class Role
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }           // "Admin", "User"
    public string Description { get; private set; }
    
    public ICollection<UserRole> UserRoles { get; private set; } = [];
}
```

### 3.3 UserRole (Many-to-Many)

```csharp
public sealed class UserRole
{
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public Guid RoleId { get; private set; }
    public Role Role { get; private set; } = null!;
    public DateTimeOffset AssignedAt { get; private set; }
    public Guid? AssignedBy { get; private set; }
}
```

### 3.4 RefreshToken Entity

```csharp
public sealed class RefreshToken
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public User User { get; private set; } = null!;
    public string TokenHash { get; private set; }      // SHA-256 of raw token
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public string? RevokedByIp { get; private set; }
    public string? ReplacedByTokenHash { get; private set; }  // for rotation chain
}
```

---

## 4. API Contracts

### 4.1 Register

**POST** `/api/auth/register`

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "fullName": "John Doe"
}
```

**Response (201):**
```json
{
  "userId": "guid",
  "email": "user@example.com",
  "fullName": "John Doe",
  "roles": ["User"]
}
```

### 4.2 Login

**POST** `/api/auth/login`

**Request:**
```json
{
  "email": "user@example.com",
  "password": "SecurePass123!",
  "rememberMe": false
}
```

**Response (200):**
```json
{
  "accessToken": "eyJ...",
  "refreshToken": "base64url...",
  "expiresIn": 900,
  "tokenType": "Bearer",
  "user": {
    "userId": "guid",
    "email": "user@example.com",
    "fullName": "John Doe",
    "roles": ["User"]
  }
}
```

### 4.3 Refresh Token

**POST** `/api/auth/refresh`

**Request:**
```json
{
  "refreshToken": "base64url..."
}
```

**Response (200):** Same as login response (new access + refresh token pair)

### 4.4 Logout

**POST** `/api/auth/logout`

**Request:**
```json
{
  "refreshToken": "base64url..."
}
```

**Response (204):** No content

### 4.5 Get Current User

**GET** `/api/auth/me`

**Response (200):**
```json
{
  "userId": "guid",
  "email": "user@example.com",
  "fullName": "John Doe",
  "roles": ["User"],
  "isEmailVerified": false,
  "createdAt": "2025-01-15T10:00:00Z"
}
```

---

## 5. Security Design

### 5.1 JWT Access Tokens

- **Algorithm:** HS256 (symmetric)
- **Lifetime:** 15 minutes (configurable)
- **Claims:** `sub` (userId), `email`, `fullName`, `roles[]`, `jti`, `iat`, `exp`
- **Signing Key:** 256-bit base64url from environment variable `JWT_SIGNING_KEY`

### 5.2 Refresh Tokens

- **Format:** 32-byte cryptographically random, base64url encoded
- **Storage:** Only SHA-256 hash stored in DB
- **Lifetime:** 7 days (configurable), 30 days if `rememberMe=true`
- **Rotation:** On each refresh, old token revoked, new token issued
- **Revocation:** On logout, password change, security event

### 5.3 Password Hashing (Argon2id)

- **Algorithm:** Argon2id (v1.3)
- **Memory:** 64 MB (configurable)
- **Iterations:** 3 (configurable)
- **Parallelism:** 4 (configurable)
- **Salt:** 16-byte cryptographically random per password
- **Output:** 32-byte hash, encoded as `$argon2id$v=19$m=65536,t=3,p=4$salt$hash`

### 5.4 CORS

- Explicit origins from configuration
- Credentials allowed only for configured origins

---

## 6. Endpoints Implementation

### 6.1 Minimal API Pattern

Each endpoint:
1. Binds input (validated via FluentValidation)
2. Enforces authorization (where applicable)
3. Calls feature service
4. Maps result to response contract
5. Returns typed `IResult` (Results.Ok, Results.Created, etc.)

### 6.2 Authorization

- `[Authorize]` attribute on protected endpoints
- Policy-based: `RequireRole("Admin")`, `RequireRole("User")`
- Custom `JwtBearerEvents` for token validation logging

---

## 7. Validation Rules

### 7.1 Register

- Email: required, valid format, unique, max 256 chars
- Password: required, min 8 chars, max 128, at least 1 upper, 1 lower, 1 digit, 1 special
- FullName: required, max 100 chars

### 7.2 Login

- Email: required, valid format
- Password: required

### 7.3 Refresh/Logout

- RefreshToken: required, valid base64url

---

## 8. Error Handling

### 8.1 Problem Details (RFC 7807)

All errors return `application/problem+json`:

```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Unauthorized",
  "status": 401,
  "detail": "Invalid credentials",
  "instance": "/api/auth/login"
}
```

### 8.2 Standard Error Codes

| Scenario | Status | Title |
|----------|--------|-------|
| Invalid credentials | 401 | Unauthorized |
| Email already exists | 409 | Conflict |
| Validation failed | 400 | Bad Request |
| Token expired | 401 | Unauthorized |
| Token revoked | 401 | Unauthorized |
| Insufficient permissions | 403 | Forbidden |

---

## 9. Configuration

### 9.1 appsettings.json Structure

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=...;Database=EbiAuth;..."
  },
  "Jwt": {
    "SigningKey": "base64url-256-bit-key",
    "Issuer": "Ebi.Api",
    "Audience": "Ebi.Client",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7,
    "RefreshTokenDaysRememberMe": 30
  },
  "Argon2": {
    "MemoryKib": 65536,
    "Iterations": 3,
    "Parallelism": 4,
    "SaltSize": 16,
    "HashSize": 32
  },
  "Cors": {
    "AllowedOrigins": ["https://localhost:3000"]
  }
}
```

### 9.2 Secrets

- `JWT_SIGNING_KEY` → User Secrets / Environment Variable
- Connection string → User Secrets / Environment Variable

---

## 10. Testing Strategy

### 10.1 Unit Tests
- Argon2 hasher: verify hash/verify roundtrip, timing resistance
- JWT service: token creation, validation, claims extraction
- Domain logic: user creation, role assignment, refresh token rotation

### 10.2 Integration Tests
- Auth endpoints: register, login, refresh, logout, me
- Database: EF Core migrations, unique constraints, cascade deletes
- Authorization: role-based access, token validation

### 10.3 API Tests
- Contract validation (request/response shapes)
- ProblemDetails format
- CORS headers

---

## 11. Migration & Deployment

### 11.1 EF Core Migrations
- Initial migration: `CreateAuthSchema`
- Run on startup (development) or via CLI (production)

### 11.2 Seed Data
- Default roles: `Admin`, `User`
- No default users (security)

---

## 12. Non-Functional Requirements

| Requirement | Target |
|-------------|--------|
| Login latency (p99) | < 500ms |
| Token refresh latency (p99) | < 200ms |
| Concurrent users | 10,000+ |
| Password hash verification | ~100ms (Argon2id tuned) |
| Token validation | < 5ms |

---

## 13. OpenAPI / Scalar

- Document all auth endpoints
- Include security scheme (Bearer JWT)
- Scalar UI at `/scalar/v1`
- OpenAPI JSON at `/openapi/v1.json`

---

## 14. Out of Scope (Future)

- Email verification flow
- Password reset flow
- MFA / 2FA
- Social login (OAuth2/OIDC)
- Impersonation
- Audit logging
- Rate limiting (add via middleware later)

---

## 15. Acceptance Criteria

1. ✅ User can register with email/password/name
2. ✅ Duplicate email returns 409 Conflict
3. ✅ User can login and receive access + refresh tokens
4. ✅ Invalid credentials returns 401 with ProblemDetails
5. ✅ Access token validates on protected endpoints
6. ✅ Expired access token returns 401
7. ✅ Refresh token rotates and invalidates old token
8. ✅ Logout revokes refresh token
9. ✅ Role-based authorization works (Admin vs User)
10. ✅ Argon2id hashing verified (timing-safe)
11. ✅ OpenAPI docs accessible at `/scalar/v1`
12. ✅ All tests pass (unit + integration)
13. ✅ Build succeeds with warnings as errors