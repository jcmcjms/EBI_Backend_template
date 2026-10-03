# AI Development Rules

Before coding:
1. read `AGENTS.md`
2. read the rule index
3. inspect the target feature
4. inspect neighboring implementations
5. determine whether an existing abstraction already solves the problem

Do not create an abstraction until its responsibility is known. Explain why it exists and why it belongs in its location.

Keep endpoints thin.

Before claiming optimization, inspect query cardinality, materialization, N+1 risk, external I/O, allocations, serialization, cache semantics, and concurrency.

Do not invent APIs, packages, configuration, database fields, or external contracts.

When a touched class approaches ~300 lines, perform a decomposition review.

Every AI task must report changes, tests, build verification, and remaining limitations.
