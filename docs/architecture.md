# Architecture

Kwestie uses a pragmatic Clean Architecture approach.

The objective is to keep business rules independent from frameworks and infrastructure while avoiding unnecessary complexity.

This document is the source of truth for architectural decisions unless a decision is explicitly revised.

## Backend Projects

### Kwestie.Domain

Contains the core business model and intrinsic business rules.

Domain must not reference:

- Kwestie.Application
- Kwestie.Infrastructure
- Kwestie.Api
- Entity Framework Core
- ASP.NET Core
- SQL Server
- ASP.NET Core Identity
- JWT-specific infrastructure

Using the .NET base class library is expected and does not violate this rule.

Typical Domain contents include:

- Entities
- Enums
- Domain exceptions
- Value objects when justified
- Domain events when justified

### Kwestie.Application

Contains application use cases, orchestration, authorization-related policies, and rules that require information from outside a single aggregate or entity.

Application depends on Domain.

Application may define contracts that it needs from external systems.

Example:

```text
Kwestie.Application
        |
        | defines / requires
        v
IKwestieRepository

Kwestie.Infrastructure
        |
        | implements
        v
IKwestieRepository
```

Application must not reference Infrastructure.

Kwestie may use a lightweight CQRS style where separating commands and queries improves clarity. No mediator library is required by the architecture.

### Kwestie.Infrastructure

Contains technical implementations required by the application.

Planned responsibilities include:

- Entity Framework Core
- SQL Server persistence
- Repository implementations
- ASP.NET Core Identity
- Password hashing
- JWT generation
- Refresh-token persistence
- External services

Infrastructure depends on Application and Domain.

### Kwestie.Api

Acts as the HTTP boundary and composition root.

Responsibilities include:

- Exposing HTTP endpoints
- Authentication and authorization middleware
- Dependency injection
- Mapping HTTP requests to application use cases
- Returning HTTP responses

Controllers should remain thin and should not contain domain business rules.

API references Application and Infrastructure because it composes concrete implementations at runtime.

## Dependency Direction

The actual source-code dependency rules are:

```text
Kwestie.Domain
└── no project dependencies

Kwestie.Application
└── Kwestie.Domain

Kwestie.Infrastructure
├── Kwestie.Application
└── Kwestie.Domain

Kwestie.Api
├── Kwestie.Application
└── Kwestie.Infrastructure
```

A simplified view is:

```text
                 Kwestie.Api
                 /         \
                v           v
Kwestie.Application <--- Kwestie.Infrastructure
                \           /
                 v         v
                  Kwestie.Domain
```

Runtime calls may reach Infrastructure through abstractions, but source-code dependencies must continue pointing inward.

## Cross-Entity and Workspace Rules

A single domain entity should not perform infrastructure queries.

Rules that require checking other data, such as:

- whether a user is an active member of a workspace
- whether a category belongs to the same workspace
- whether an assignee is eligible for assignment

will be orchestrated by Application using information obtained through abstractions.

Domain still owns intrinsic rules that can be enforced using the entity's own state.

## Monorepo

Kwestie uses a monorepo containing both frontend and backend.

```text
Kwestie/
├── src/
│   ├── backend/
│   └── frontend/
├── tests/
├── docs/
└── Kwestie.slnx
```

This does not require frontend and backend to be deployed together.

They remain independently buildable and may later be deployed to separate services or separate containers.

## Frontend

The frontend uses Angular with standalone components.

The intended organization is feature-oriented:

```text
src/app/
├── core/
├── shared/
├── features/
├── layout/
├── app.config.ts
└── app.routes.ts
```

NgRx is not part of the current implementation and should only be introduced if application state becomes complex enough to justify it.

## Authentication

The accepted direction for authentication is:

```text
ASP.NET Core Identity
        +
JWT access token
        +
refresh token
```

This is planned and not yet implemented.

OAuth 2.0 / OpenID Connect may be introduced later if Kwestie needs external identity providers, enterprise SSO, or third-party clients.

## Persistence

SQL Server through Entity Framework Core is the accepted persistence direction.

Persistence is not yet implemented.

Persistence configuration belongs in Infrastructure.

Domain must not contain EF Core attributes or persistence-specific dependencies.

EF Core Fluent API will be preferred for entity configuration.

## API Style

Kwestie will use pragmatic REST.

Normal resource operations may use standard REST endpoints, while explicit domain actions may use action-oriented endpoints when that better represents the use case.

Any endpoint examples in documentation are illustrative until the corresponding application use case is implemented.

## Testing

The solution currently contains:

- `Kwestie.Domain.Tests`
- `Kwestie.Application.Tests`
- `Kwestie.IntegrationTests`

At the current stage, meaningful automated coverage exists in `Kwestie.Domain.Tests`.

The Application and Integration test projects are scaffolding for future work and should not be interpreted as completed test coverage.

Domain tests should run without database, API, or infrastructure dependencies.

## Working Agreement

When implementing a feature:

- Respect the documented architecture.
- Do not silently change an existing architectural decision.
- Do not introduce new dependencies or patterns without a concrete need.
- If a documented decision appears to need revision, explain the reason before implementing the change.
- Keep changes focused on the requested scope.
- Keep documentation aligned with the implementation.
