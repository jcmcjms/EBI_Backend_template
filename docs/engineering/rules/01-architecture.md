# Architecture Rules

Use a modular monolith organized by business feature.

Preferred shape:

```text
src/<Api>/
├── Composition/
├── Features/
│   └── <Feature>/
│       ├── Contracts/
│       ├── Endpoints/
│       ├── Services/
│       ├── Domain/
│       ├── Persistence/
│       ├── Validation/
│       └── Jobs/
├── Infrastructure/
└── Shared/
```

Create only the folders a feature needs.

Dependency direction:

```text
Endpoint → Feature Service/Use Case → Domain/Persistence → Infrastructure/External System
```

Endpoints handle binding, authorization, validation, invocation, and response mapping. Business workflows do not belong in endpoints.

Do not create root-level `Common`, `Helpers`, `Utils`, `Utilities`, `Managers`, `Models`, `Repositories`, or generic `Services` folders.

Start with one API project and one test project. Split projects only when project-level dependency isolation has a demonstrated benefit.

Do not introduce CQRS, MediatR, generic repositories, or ceremony merely for architectural fashion.
