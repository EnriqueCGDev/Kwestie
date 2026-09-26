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

The first implemented use case is Create Kwestie. Its handler creates the Domain entity using a generated ID and .NET `TimeProvider`, awaits `IKwestieRepository.AddAsync`, and returns the entity ID. Infrastructure implements and registers that repository with an EF Core SQL Server context. There is no Create Kwestie API endpoint or handler registration yet.

Current contract and implementation:

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

Implemented responsibilities include EF Core SQL Server persistence, Fluent API mapping, and `KwestieRepository`. `AddInfrastructure` registers the context and repository; the API supplies `ConnectionStrings:Kwestie` from configuration.

Infrastructure also contains `ApplicationUser : IdentityUser<Guid>` and registers Identity Core with EF stores using the same `KwestieDbContext`. `ApplicationUser` is not a Domain entity. Neither Domain nor Application depends on Identity types.

Planned responsibilities include:

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

Identity is implemented with `AddIdentityCore<ApplicationUser>` and EF stores. Register is implemented in Application through IUserRegistration, with an Infrastructure adapter using UserManager. Application remains independent of Identity types. The adapter uses UserName = Email and Identity requires unique email while retaining default password policies. No authentication cookies, JWT, external providers, or endpoints are registered. Login and token issuance remain pending, so authentication is not complete.

The context uses `IdentityUserContext<ApplicationUser, Guid>` without global roles. Future Workspace Admin/Member roles are separate domain concepts, not global Identity roles. No roles are registered or seeded. `20260925192607_AddIdentity` was applied manually to the local `Kwestie` database; Register required no additional migration. Login, JWT, and refresh tokens remain unimplemented.

OAuth 2.0 / OpenID Connect may be introduced later if Kwestie needs external identity providers, enterprise SSO, or third-party clients.

## Persistence

SQL Server through Entity Framework Core is the accepted persistence direction.

The persistence implementation and `InitialCreate` migration are present. The migration was applied locally to the existing `Kwestie` database. A real repository round-trip test verifies insertion, SQL Server IDENTITY generation, EF's update of `Number`, and retrieval through a separate DbContext.

`AddIdentity` also exists and was applied manually to the same database. AspNetUsers, AspNetUserClaims, AspNetUserLogins, and AspNetUserTokens now exist physically in SQL Server, and the migration is recorded in `__EFMigrationsHistory`.

Persistence configuration belongs in Infrastructure.

Domain must not contain EF Core attributes or persistence-specific dependencies.

EF Core Fluent API configures the current entity. Migrations belong to Infrastructure and are applied manually; tests do not create databases or apply migrations. See [Infrastructure Layer](infrastructure.md) for configuration and scope.

## API Style

Kwestie will use pragmatic REST.

Normal resource operations may use standard REST endpoints, while explicit domain actions may use action-oriented endpoints when that better represents the use case.

Any endpoint examples in documentation are illustrative until the corresponding application use case is implemented.

## Testing

The solution currently contains:

- `Kwestie.Domain.Tests`
- `Kwestie.Application.Tests`
- `Kwestie.IntegrationTests`

At the current stage, meaningful automated coverage exists in all three test projects.

Application tests cover Create Kwestie using a small repository fake and a controlled .NET `TimeProvider`, and Register using an `IUserRegistration` fake, without mocking libraries.

IntegrationTests contains:

- EF model and materialization checks that do not require a database.
- A real `KwestieRepository` round-trip test against SQL Server, including generated `Number` and retrieval through a separate DbContext.
- A real user-registration test against SQL Server through the `IUserRegistration` implementation and Identity's `UserManager`. It verifies persistence in `AspNetUsers`, retrieval through a separate DbContext, normalized Email/UserName values, an Identity-generated `PasswordHash`, password validation through Identity's password hasher, and duplicate-email rejection.

The real tests require the existing local `Kwestie` database with `InitialCreate` and `AddIdentity` already applied, and `ConnectionStrings:Kwestie` from the API's shared .NET User Secrets. They remove their created Kwestie or user in `finally`, even if an assertion fails after insertion. They do not create the database or apply migrations automatically.

Domain and current Application tests run without database, API, or infrastructure dependencies.

## Working Agreement

When implementing a feature:

- Respect the documented architecture.
- Do not silently change an existing architectural decision.
- Do not introduce new dependencies or patterns without a concrete need.
- If a documented decision appears to need revision, explain the reason before implementing the change.
- Keep changes focused on the requested scope.
- Keep documentation aligned with the implementation.
