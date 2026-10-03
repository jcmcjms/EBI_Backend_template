# Change Management Rules

Separate behavior changes, structural refactors, dependency upgrades, and schema migrations when practical.

New packages require a concrete reason. Prefer framework-native .NET 10 functionality.

For architectural patterns, new infrastructure subsystems, project boundaries, or external integrations, create an ADR under `docs/architecture/decisions/`.

Database changes require migration, data-impact review, forward/rollback strategy, and tests where practical.

A change is complete only when architecture, tests, build/analyzers, security, performance implications, and relevant documentation are addressed.
