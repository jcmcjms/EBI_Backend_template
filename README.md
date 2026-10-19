# Backend Template — .NET 10 / C# 14, SQL Server, JWT Auth + RBAC (no ASP.NET Core Identity)

A production-shaped starting point for ASP.NET Core backends, built strictly under the engineering
rules in [`AGENTS.md`](AGENTS.md) and [`docs/engineering/rules/`](docs/engineering/rules/).

Authentication and role-based access control are implemented directly on framework primitives —
no Identity, no MediatR, no generic repositories, no catch-all folders. Minimal APIs, feature-first
modular monolith, EF Core with the real SQL Server provider, RFC 9457 Problem Details everywhere.

## What's included

- **JWT authentication** (HS256) with short-lived access tokens (default 15 min) and
  **rotating refresh tokens** (default 14 days) with **reuse detection**: replaying a rotated
  token revokes every session of that user. Login accepts **email or username**.
- **User profiles**: username (unique login handle), first/middle/last name, suffix, and job
  title — admin-managed via create and a full-replacement profile endpoint.
- **RBAC**: a closed, code-defined permission catalog (`users.read`, `users.write`,
  `users.manage`, `roles.read`, `roles.manage`), roles stored in the database, permissions carried
  as signed `perm` claims and enforced per endpoint via `.RequirePermission(...)`.
- **User administration**: create users, assign roles, disable/enable, admin password reset,
  self-service password change — each with the session revocation semantics described below.
- **PBKDF2 (SHA-256, 600k iterations, per-hash parameters)** password hashing using
  `Rfc2898DeriveBytes.Pbkdf2` — framework-native, no crypto packages, with transparent rehash on
  login when parameters are raised.
- **Hardened pipeline**: per-IP rate limiting on credential endpoints, bounded offset pagination,
  request validation (.NET 10 built-in), Problem Details for every error path, no internal details
  in responses, liveness/readiness health split, HSTS + HTTPS redirection outside Development.
- **Startup safety**: all critical options (signing key, lifetimes, hashing parameters, connection
  string, rate limits) are validated when the host starts. The public development signing key is
  **rejected in Production**.
- **EF Core migrations** with a generated initial schema, plus a seeder for the system `Admin`
  role (kept in sync with the code catalog) and an optional bootstrap admin user.

## Quickstart (Docker)

```bash
# .env (local development only — override everything in real environments)
cat > .env <<'EOF'
AUTH_SIGNING_KEY=replace-with-a-secret-key-of-at-least-32-chars
SEED_ADMIN_PASSWORD=replace-with-a-strong-local-password
EOF

docker compose up --build
```

- API: `http://localhost:8080` (OpenAPI document: `http://localhost:8080/openapi/v1.json` in Development)
- SQL Server: `localhost:1433` (`sa` / compose password, development only)
- Migrations + seeding run automatically in the compose setup (`Database:ApplyMigrations=true`).

```bash
TOKEN=$(curl -s http://localhost:8080/api/auth/login \
  -H 'Content-Type: application/json' \
  -d '{"identifier":"admin","password":"<SEED_ADMIN_PASSWORD>"}' | python3 -c 'import sys,json;print(json.load(sys.stdin)["accessToken"])')

curl -s http://localhost:8080/api/users/me -H "Authorization: Bearer $TOKEN"
```

`identifier` accepts the username (default seed: `admin`) or the email address.

## Quickstart (local SDK + Docker SQL Server)

```bash
docker compose up -d sqlserver                       # SQL Server only
cd src/Template.Api
dotnet user-secrets init                             # or use environment variables
dotnet user-secrets set "Auth:SigningKey" "<at-least-32-char-secret>"
dotnet user-secrets set "Seed:AdminPassword" "<strong-local-password>"
dotnet run                                           # Development profile: migrates + seeds on startup
```

`appsettings.Development.json` provides local defaults (localhost connection string, admin seed
email, development signing key). The development signing key is refused when the environment is
Production, and secrets for real deployments must come from environment variables or a secret store.

## Configuration reference

| Key | Required | Default | Notes |
| --- | --- | --- | --- |
| `ConnectionStrings:Database` | yes | — | SQL Server connection string. Startup fails when missing. |
| `Database:ApplyMigrations` | no | `false` | Applies migrations + runs the seeder at startup. For development and one-shot deployment jobs only (see Deployment). |
| `Auth:Issuer`, `Auth:Audience` | yes | `Template.Api` / `Template.Api.Clients` | JWT issuer/audience validation. |
| `Auth:SigningKey` | yes | — | HS256 key, ≥ 32 chars. The shipped development key is rejected in Production. |
| `Auth:AccessTokenLifetime` | no | `00:15:00` | Must be 1 min – 1 h. Bounds permission-claim staleness. |
| `Auth:RefreshTokenLifetime` | no | `14.00:00:00` | Must be 1 h – 90 d and exceed the access lifetime. |
| `PasswordHasher:IterationCount` | no | `600000` | PBKDF2-SHA256 iterations; ≥ 100 000. Stored hashes keep their original parameters and are upgraded on next login. |
| `PasswordPolicy:MinLength` / `MaxLength` | no | `12` / `128` | Enforced by user-creation/password use cases. |
| `RateLimiting:AuthenticationPermitLimit` / `AuthenticationWindow` | no | `10` / `00:01:00` | Fixed window per client IP on `/api/auth/*`. |
| `Seed:AdminEmail` / `Seed:AdminPassword` | no | — | When both set (and migrations applied at startup), an admin user with the system `Admin` role is created if absent. |
| `Seed:AdminUsername` | no | email local part | Login handle for the seeded admin; the derived default must be a valid username or startup fails. |
| `Cors:AllowedOrigins` | no | `[]` | Explicit origins only; CORS middleware is registered only when the list is non-empty. |

## Endpoints

All errors are RFC 9457 Problem Details. `Perm` column = required permission claim.

| Method & path | Auth | Perm | Behavior |
| --- | --- | --- | --- |
| `POST /api/auth/login` | anonymous (rate limited) | — | Body: `{ identifier, password }` — identifiers containing `@` match **emails**, all others match **usernames** (usernames can never contain `@`, so matching is unambiguous). 200 token pair; 401 generic "Invalid credentials" for unknown identifier, wrong password, **and** disabled accounts (reason logged server-side only) |
| `POST /api/auth/refresh` | anonymous (rate limited) | — | Rotates the refresh token; reuse of a rotated token → 401 **and all user sessions revoked** |
| `POST /api/auth/logout` | anonymous (rate limited) | — | Revokes the presented refresh token; idempotent (204 even for unknown tokens) |
| `GET /api/users/me` | any authenticated | — | Own profile: identity + roles/permissions from validated claims, profile fields (username, names, suffix, job title) from a single primary-key database read — fresh without bloating every token |
| `POST /api/users/me/password` | any authenticated | — | Requires current password; revokes all sessions afterwards |
| `GET /api/users?page&pageSize` | authenticated | `users.read` | Paged list (defaults 1/25, max page size 100, deterministic order) |
| `POST /api/users` | authenticated | `users.write` | Admin creates user (no public registration by design). Required: email, username (3–64, `[a-zA-Z0-9._-]`), first name, last name, password; optional: middle name, suffix, job title, role ids. 409 on duplicate email **or username**; 400 on weak password / invalid username / unknown roles |
| `GET /api/users/{id}` | authenticated | `users.read` | 404 Problem Details when absent |
| `PATCH /api/users/{id}` | authenticated | `users.manage` | Email and/or `isDisabled`; disabling revokes sessions; cannot disable yourself (409) |
| `PUT /api/users/{id}/profile` | authenticated | `users.manage` | Full replacement of username/first/middle/last/suffix/job title (omitted optional fields are cleared); 409 on username conflict; new username works for login immediately |
| `PATCH /api/users/{id}/roles` | authenticated | `users.manage` | Full replacement of role assignments |
| `POST /api/users/{id}/password` | authenticated | `users.manage` | Admin reset; revokes all sessions |
| `GET /api/roles?page&pageSize` | authenticated | `roles.read` | Paged list with permissions + assignment counts |
| `POST /api/roles` | authenticated | `roles.manage` | Permission names validated against the catalog (400 lists unknown values); 409 duplicate name |
| `PUT /api/roles/{id}` | authenticated | `roles.manage` | Full replacement update; system roles immutable (409) |
| `DELETE /api/roles/{id}` | authenticated | `roles.manage` | 409 while assigned to users or for system roles; DB FK is `RESTRICT` as a second guard |
| `GET /api/permissions` | any authenticated | — | The code-defined catalog (not secret — it is already in everyone's claims) |
| `GET /health/live` | anonymous | — | Process liveness; never touches dependencies |
| `GET /health/ready` | anonymous | — | SQL Server connectivity, bounded 3 s timeout |

User reads (list item, detail, and write responses) share one admin read model, `UserResponse`:
`id, email, username, firstName, middleName, lastName, suffix, jobTitle, isDisabled, createdAt,
disabledAt, roles[]`.

## RBAC model and extension points

Permissions are a **closed catalog in code** (`Infrastructure/Authorization/Permissions.cs`) because
endpoints reference them statically; the database stores which **roles** grant which catalog
permissions. To add a capability:

1. Add a constant to `Permissions` (it joins `Permissions.All` automatically).
2. Guard the endpoint: `.RequirePermission(Permissions.YourNew)`.
3. Deploy — the seeder syncs the system `Admin` role with the catalog at startup; grant the new
   permission to custom roles via `PUT /api/roles/{id}`.

Authorization reads `perm` claims from the signed access token (which also carries `sub`, `email`,
and `username`), so role changes take effect when
the user's next access token is issued (bounded by `AccessTokenLifetime`); refresh tokens are
revoked on disable and password changes, which blocks session renewal immediately.

Session lifecycle summary:

| Event | Refresh sessions | Access token |
| --- | --- | --- |
| Logout | presented token revoked | expires naturally |
| Refresh | old token revoked (rotation), reuse → all revoked | new one issued |
| Disable user | all revoked | expires naturally (≤ 15 min default) |
| Password reset/change | all revoked | expires naturally |
| Role change | kept | stale claims until next issuance/expiry |

## Project layout

```text
src/Template.Api/
├── Program.cs                     # thin entry; composition root calls below
├── Composition/                   # all DI registration + pipeline (the composition root)
├── Features/
│   ├── Auth/                      # login, refresh rotation, logout (+ Domain/, Persistence/)
│   ├── Users/                     # profile, admin user management (+ Domain/, Persistence/)
│   └── Roles/                     # role & permission administration (+ Domain/, Persistence/)
└── Infrastructure/
    ├── Authorization/             # permission catalog, requirement, handler, endpoint convention
    ├── Health/                    # bounded SQL Server readiness check
    ├── Http/                      # Problem Details writer, unhandled-exception handler
    ├── Observability/             # source-generated log messages (stable event ids)
    ├── Paging/                    # bounded pagination contract
    ├── Persistence/               # AppDbContext, design-time factory, seeder, migrations
    └── Security/                  # password hashing, JWT issuer, refresh-token primitives, options

tests/Template.Api.Tests/
├── Security/  Authorization/  Domain/  Persistence/   # unit tests (no DB)
├── Api/                                               # HTTP pipeline tests (real host, no DB)
└── Integration/                                       # full flows against real SQL Server
```

Entities live with the feature that owns them; there is one `AppDbContext` and no repository
layer — use cases query EF Core directly, endpoints only bind → authorize → validate → invoke →
map.

## Running the tests

```bash
dotnet test
```

- **Unit + pipeline tests always run** (60 tests here): password hashing, refresh-token hashing,
  JWT issuance/validation, authorization handler, domain invariants, EF model validation against
  the SQL Server provider, and HTTP-level tests of the real pipeline (401/403/400/500 Problem
  Details shapes, claim-based `/me`, health split) — all without a database.
- **Integration tests (15) require SQL Server** and are skipped with an explicit message when it
  is not configured. They cover seeding, login, token rotation + reuse detection, logout, password
  change/reset, disable/enable, RBAC enforcement end-to-end, role lifecycle guards, and pagination
  against the real provider. To run them:

```bash
docker compose up -d sqlserver
export TEMPLATE_API_TEST_SQLSERVER="Server=localhost,1433;User Id=sa;Password=LocalDevPassword1!;TrustServerCertificate=True"
dotnet test
```

The fixture creates a uniquely named database (`tpl_it_<guid>`), migrates and seeds it through the
application's real startup path, and drops it afterwards. EF Core InMemory is deliberately **not**
used anywhere: it cannot prove relational behavior.

## Migrations workflow

```bash
dotnet tool restore                                   # dotnet-ef is a local tool (pinned)
dotnet ef migrations add <Name> --project src/Template.Api \
  --output-dir Infrastructure/Persistence/Migrations
dotnet ef migrations script --project src/Template.Api  # review the generated DDL
dotnet ef database update --project src/Template.Api    # apply with a real connection string
```

Generated migration and snapshot files are never hand-edited (analyzer rules are scoped off for
that folder only). Schema changes belong in their own reviewed change, per
`docs/engineering/rules/08-change-management.md`.

The `AddUserProfileFields` migration is expand-safe for databases created from `InitialSchema`:
`Username` is added nullable with a **filtered** unique index (`WHERE Username IS NOT NULL`), and
`FirstName`/`LastName` are added `NOT NULL DEFAULT N''`. Pre-existing rows therefore keep working
(login by email, `Username` simply never matches `NULL`); give them usernames and real names via
`PUT /api/users/{id}/profile`.

## Deployment notes

- **Migrations**: `Database:ApplyMigrations=true` is for development and one-shot jobs. For
  rolling deployments, apply migrations out-of-band (CI step or job) and keep schema changes
  backward-compatible (expand/contract) while old and new versions overlap.
- **Secrets**: `Auth:SigningKey`, `Seed:AdminPassword`, and the connection string must come from
  environment variables or a platform secret store. Startup fails fast on missing/weak values, and
  the development key is rejected in Production.
- **Multiple instances**: the API is stateless; refresh-token state lives in SQL Server. Startup
  seeding is idempotent and race-tolerant (unique indexes + retry-as-noop).
- **Clocks**: JWT validation uses 30 s clock skew; keep instance time synchronized.

## Known limitations (deliberate, documented)

- **No refresh-token cleanup job**: revoked/expired rows accumulate in `RefreshTokens`. Add a
  background cleanup when volume warrants it (durable background processing per
  `docs/engineering/rules/10-production-operations.md`).
- **Permission claims staleness**: role changes reach clients at the next token issuance; the
  window is bounded by `AccessTokenLifetime`. Per-request database permission checks were judged
  not worth the round trip for this template. The `username` claim has the same staleness window
  after a profile rename (login by the new username works immediately; it is database-backed).
- **Offset pagination** for admin lists: intentional (arbitrary page navigation, small bounded
  tables); switch to keyset pagination if a listed collection becomes high-volume.
- **No public self-registration**: user creation is an administrative act (`users.write`), because
  role assignment must be a deliberate server-side decision in an RBAC system.

## Engineering rules

This repository ships the ruleset it was built under: read [`AGENTS.md`](AGENTS.md) first, then
[`docs/engineering/rules/00-index.md`](docs/engineering/rules/00-index.md). Every code change is
subject to `docs/engineering/rules/11-ai-slop.md`.
