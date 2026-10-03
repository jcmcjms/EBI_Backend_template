# Testing Rules

Every new business behavior gets automated tests.

Use:
- unit tests for deterministic business rules
- integration tests for database/provider behavior
- API tests for transport/auth/serialization
- end-to-end tests only for critical cross-system flows

Prioritize authorization, data isolation, concurrency, idempotency, validation, persistence consistency, integrations, cancellation, and error handling.

Do not optimize for coverage percentage. Avoid tests coupled to implementation details.

Do not use EF Core InMemory as proof of SQL Server behavior.
