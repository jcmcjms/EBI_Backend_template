# C# 14 / .NET 10 Rules

Baseline:
- `net10.0`
- C# `14.0`
- nullable enabled
- implicit usings enabled
- .NET analyzers enabled
- warnings treated as errors

Prefer file-scoped namespaces, sealed types when inheritance is unnecessary, guard clauses, pattern matching when clearer, collection expressions when clearer, `required` where construction must be explicit, and `TimeProvider` for time.

Async rules:
- All I/O is asynchronous.
- No `.Result`.
- No `.Wait()`.
- No `Thread.Sleep`.
- No `Task.Run` for ordinary HTTP request work.
- No `async void`.
- Propagate `CancellationToken`.

Avoid deep inheritance, static mutable state, service locators, reflection-heavy runtime behavior without justification, and magic strings where typed constructs are clearer.

Use new C# 14 features only when they improve clarity or measurable performance.
