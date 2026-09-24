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

### Planned

- Entity Framework Core
- SQL Server
- ASP.NET Core Identity
- JWT access tokens
- Refresh tokens
- Integration testing against real infrastructure
- Docker
- CI/CD

## Current Status

Kwestie is under active development.

The current milestone covers the core domain model and the first application use case, before real persistence and authentication are introduced.

Implemented so far:

- Base Clean Architecture solution
- Angular frontend scaffold
- Domain and test projects
- Initial `Kwestie` entity
- `Open -> InProgress -> Resolved -> Closed` lifecycle
- Unit tests for the current domain behavior
- Create Kwestie application use case and its unit tests

Create Kwestie uses an Application-defined repository contract; no real persistence implementation or Create Kwestie API endpoint exists yet. Workspace and membership checks required before exposing this use case are not implemented.

Persistence, authentication, workspaces, assignment, comments, history, search, and dashboard functionality are not implemented yet.

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
│   └── domain.md
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

The current Domain and Application tests do not require SQL Server, EF Core, or any external infrastructure. The Integration test project still contains only a placeholder test.

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
- [Domain Model](docs/domain.md)

These documents evolve with the implementation and act as the current source of truth for architectural and domain decisions.
