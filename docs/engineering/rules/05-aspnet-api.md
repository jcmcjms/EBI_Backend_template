# ASP.NET Core API Rules

Use Minimal APIs consistently.

Endpoints should:
1. bind input
2. enforce authorization
3. validate
4. call the feature service/use case
5. map the result

Use feature-local request/response contracts. Never expose EF entities directly.

Use Problem Details for failures. Never expose stack traces or internal exception details.

Every endpoint reaching I/O must accept and propagate `CancellationToken`.

Use `IHttpClientFactory` for outbound HTTP.

Keep middleware explicit and security-sensitive ordering visible.

Prefer typed results over `object`.

Do not enable caching for authenticated user-specific responses without proving the cache key and authorization semantics are safe.

Public API contract changes require review.
