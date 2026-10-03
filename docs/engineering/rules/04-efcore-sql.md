# EF Core 10 / SQL Server Rules

Normal read-only queries should:
- use `AsNoTracking()`
- project to required fields
- filter in SQL
- use deterministic ordering
- have bounded results

Every collection endpoint needs pagination semantics. Prefer keyset pagination for high-volume sequential navigation; use offset pagination when arbitrary page navigation is required.

Prefer projections over broad `Include` graphs. Avoid cartesian explosion and N+1 access.

Use set-based updates for large updates where appropriate. Do not load thousands of entities just to modify one field.

Use scoped `DbContext` for request work. Use `IDbContextFactory` only when genuinely independent contexts are required.

Add indexes from real query patterns, not speculation.

Use parameterized SQL. Raw SQL is an exception requiring a concrete reason.

EF migration generated files are not manually edited.
