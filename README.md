# Kwestie

Kwestie is a full-stack application for managing issues, requests, and internal work within teams.

The project aims to provide a simple and focused workflow for creating, assigning, tracking, discussing, and resolving work without reproducing the complexity of larger project-management platforms.

Kwestie is also being developed as a public portfolio project focused on maintainable architecture, clear domain modeling, testing, and modern full-stack development practices.

## Current Stack

### Implemented

- .NET 10
- ASP.NET Core
- Angular 22
- TypeScript
- SCSS
- xUnit
- Clean Architecture project structure
- EF Core / SQL Server persistence infrastructure
- Real SQL Server repository integration test
- ASP.NET Core Identity base infrastructure (Guid users and EF stores)
- Register use case with real SQL Server user-persistence test (no HTTP endpoint)

### Planned

- Login use case
- JWT access tokens
- Refresh tokens
- Docker
- CI/CD

## Current Status

Kwestie is under active development.

The current milestone covers the core domain model, application use cases, SQL Server persistence, and ASP.NET Core Identity registration. `20260925192607_AddIdentity` was applied manually to the local `Kwestie` database, which contains AspNetUsers, AspNetUserClaims, AspNetUserLogins, and AspNetUserTokens. Register is implemented in Application and Infrastructure with real user-persistence coverage, but has no HTTP endpoint. Authentication remains incomplete: Login, JWT, and refresh tokens are not implemented.

Implemented so far:

- Base Clean Architecture solution
- Angular frontend scaffold
- Domain and test projects
- Initial `Kwestie` entity
- `Open -> InProgress -> Resolved -> Closed` lifecycle
- Unit tests for the current domain behavior
- Create Kwestie application use case and its unit tests

Create Kwestie has an Infrastructure repository implementation using EF Core and SQL Server. `InitialCreate` exists and was applied locally to the existing `Kwestie` database. A real repository round-trip test verifies insertion, generated Number, retrieval, and cleanup. There is still no Create Kwestie API endpoint; workspace and membership checks required before exposing this use case are not implemented.

Full authentication is not implemented yet. Login, JWT, refresh tokens, workspaces, assignment, comments, history, search, and dashboard functionality remain pending.

## Architecture

The backend follows a pragmatic Clean Architecture approach.

```text
Kwestie.Domain
    ^
    |
Kwestie.Application
    ^
    |
Kwestie.Infrastructure

Kwestie.Api -> Kwestie.Application
Kwestie.Api -> Kwestie.Infrastructure
Kwestie.Infrastructure -> Kwestie.Application
Kwestie.Infrastructure -> Kwestie.Domain
Kwestie.Application -> Kwestie.Domain
```

The goal is to preserve dependency boundaries and testability without introducing unnecessary abstractions.

See [docs/architecture.md](docs/architecture.md) for the complete architectural rules.

## Project Structure

```text
Kwestie/
├── src/
│   ├── backend/
│   │   ├── Kwestie.Api/
│   │   ├── Kwestie.Application/
│   │   ├── Kwestie.Domain/
│   │   └── Kwestie.Infrastructure/
│   │
│   └── frontend/
│       └── kwestie-web/
│
├── tests/
│   ├── Kwestie.Domain.Tests/
│   ├── Kwestie.Application.Tests/
│   └── Kwestie.IntegrationTests/
│
├── docs/
│   ├── architecture.md
│   ├── application.md
│   ├── domain.md
│   └── infrastructure.md
│
├── Kwestie.slnx
└── README.md
```

## Planned Core Features

- Authentication
- Workspaces
- Workspace members and roles
- Categories
- Kwesties
- Assignment
- Priority management
- Status workflow
- Comments
- Functional history
- Search and filtering
- Workspace dashboard

These features will be implemented incrementally and may be refined as their domain rules are defined.

## Running the Current Tests

Requirements:

- .NET 10 SDK

From the repository root:

```bash
dotnet test
```

The current Domain and Application tests do not require SQL Server, EF Core, or any external infrastructure. IntegrationTests retains database-free EF model/materialization checks and runs real SQL Server tests for Kwestie persistence and user registration, including hashing and duplicate-email rejection. Full `dotnet test` requires the existing local `Kwestie` database with `InitialCreate` and `AddIdentity` applied and `ConnectionStrings:Kwestie` configured in the API's shared .NET User Secrets. The real tests clean up their own data in `finally`; they do not create the database or apply migrations.

API and IntegrationTests use the same `UserSecretsId`; do not store the connection string or passwords in the repository. The development SQL Server currently runs in Docker, independently of any application Docker configuration in this repository. `dotnet build` does not require SQL Server. See [Infrastructure configuration](docs/infrastructure.md#configuration).

## Development Principles

- Business rules belong in Domain when they are intrinsic to the model.
- Application orchestrates use cases and cross-entity policies.
- Infrastructure concerns remain outside Domain and Application.
- Controllers should remain thin.
- Features are introduced incrementally.
- New abstractions and dependencies are added only when they solve a concrete problem.
- Meaningful domain behavior should be covered by automated tests.
- Planned behavior should not be treated as implemented behavior.

## Documentation

- [Architecture](docs/architecture.md)
- [Application Layer](docs/application.md)
- [Infrastructure Layer](docs/infrastructure.md)
- [Domain Model](docs/domain.md)

These documents evolve with the implementation and act as the current source of truth for architectural and domain decisions.
