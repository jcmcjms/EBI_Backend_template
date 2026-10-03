# Mandatory AI Engineering Contract

This file is the entry point for AI-assisted development.

## Rule

**Do not modify source code until the applicable engineering rules have been read.**

Read `docs/engineering/rules/00-index.md`, then the relevant rule files.

## Architecture contract

This repository is a modular monolith organized by feature.

Mandatory dependency direction:

```text
HTTP Endpoint
    ↓
Feature Service / Use Case
    ↓
Feature Persistence / External Boundary
    ↓
Infrastructure / External System
```

Business behavior must not leak into endpoint handlers.

## Non-negotiable implementation rules

- Do not introduce `Common/`, `Helpers/`, `Utils/`, or `Managers/` catch-all folders.
- Do not introduce a repository interface solely because "clean architecture" says every database class needs one.
- Do not add MediatR/CQRS or another mediator solely for style.
- Do not block asynchronous work with `.Result`, `.Wait()`, or equivalent.
- Do not use `Task.Run` to make synchronous work look asynchronous in ASP.NET request paths.
- Do not use `Thread.Sleep`.
- Do not instantiate `HttpClient` manually.
- Do not create unbounded database queries or return unbounded collections.
- Read-only EF Core queries must normally use `AsNoTracking()`.
- Queries should project only the required fields.
- Every asynchronous I/O boundary must propagate `CancellationToken`.
- Endpoint handlers must stay thin.
- Do not expose EF entities directly from public API contracts.
- Do not log credentials, tokens, secrets, or unnecessary sensitive data.
- Do not manually edit generated EF migration designer files.
- Do not silently alter API or database contracts.
- Do not claim performance improvements without evidence.
- Production application classes should normally stay below ~300 lines unless there is a strong cohesion reason.
- Generated files are exempt from the line-count rule.
- Prefer the simplest design that preserves correctness, security, performance, and scalability.

## AI completion contract

Before finishing a task, report:

- files changed,
- behavior changed,
- tests run,
- build status,
- unresolved risks,
- migration/configuration implications.

Never claim verification that did not occur.
