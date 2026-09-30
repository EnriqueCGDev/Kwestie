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
- Register endpoint with real SQL Server user-persistence coverage
- Login endpoint with real SQL Server credential-validation coverage
- JWT access-token issuance from Login and API Bearer validation, with JWT and real Login + JWT tests
- Refresh-token issuance and rotation validated against SQL Server, with AddRefreshTokens applied locally
- Refresh endpoint and secure refresh-token cookie handling
- HTTP integration coverage using the real API and SQL Server

### Planned

- Logout
- Angular authentication integration
- CORS/frontend configuration according to deployment
- Docker
- CI/CD

## Current Status

Kwestie is under active development.

InitialCreate, AddIdentity, and 20260929220522_AddRefreshTokens are applied to the local Kwestie database. Login returns access and refresh tokens with separate UTC expirations, and Refresh rotates the persisted token. The complete Login + Refresh + JWT flow has been validated against SQL Server, including issuance, hash-only persistence, rotation, reuse rejection, expiration, and concurrency. API exposes Register, Login, and Refresh endpoints. Access tokens are returned as JSON for Bearer use; refresh tokens are sent only in a secure HttpOnly cookie. Logout, Angular authentication integration, and CORS remain unimplemented. Authentication is not complete.

Implemented so far:

- Base Clean Architecture solution
- Angular frontend scaffold
- Domain and test projects
- Initial `Kwestie` entity
- `Open -> InProgress -> Resolved -> Closed` lifecycle
- Unit tests for the current domain behavior
- Create Kwestie application use case and its unit tests

Create Kwestie has an Infrastructure repository implementation using EF Core and SQL Server. `InitialCreate` exists and was applied locally to the existing `Kwestie` database. A real repository round-trip test verifies insertion, generated Number, retrieval, and cleanup. There is still no Create Kwestie API endpoint; workspace and membership checks required before exposing this use case are not implemented.

Full authentication is not implemented yet. Logout, Angular authentication integration, workspaces, assignment, comments, history, search, and dashboard functionality remain pending.

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
│   ├── api.md
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

From the repository root, with the local database and User Secrets configured as described below:

```bash
dotnet build
dotnet test
```

Domain and Application tests require no SQL Server or Infrastructure. IntegrationTests includes database-free EF, JWT, and refresh mapping/configuration checks, alongside real SQL tests. Full dotnet test requires InitialCreate, AddIdentity, and AddRefreshTokens applied to the local Kwestie database and ConnectionStrings:Kwestie in shared API User Secrets. All three migrations are applied locally. The full suite passed on 2026-09-30: 99 tests, 99 passed, 0 failed, 0 skipped, including the real refresh-token SQL tests and six HTTP integration tests. Tests never create the database or apply migrations; they clean up only their own data in finally.

API and IntegrationTests use the same `UserSecretsId`; do not store the connection string or passwords in the repository. The development SQL Server currently runs in Docker, independently of any application Docker configuration in this repository. `dotnet build` does not require SQL Server. See [Infrastructure configuration](docs/infrastructure.md#configuration).

JWT tests use separate public test configuration and do not need the developer's signing key. They verify generation, cryptographic/Bearer validation, rejected tokens, and startup settings. SQL tests have passed for refresh issuance, hash-only persistence, rotation, reuse rejection, expiration, concurrent consumption, and Login + Refresh + JWT. The RefreshTokens table exists locally following application of 20260929220522_AddRefreshTokens.

To start the API locally, also configure Jwt:Key through User Secrets; issuer, audience, and the default 15-minute lifetime are in appsettings.json. No signing key is stored in the repository. See the command and requirements in [JWT configuration](docs/infrastructure.md#jwt-configuration).

RefreshTokens:LifetimeDays defaults to 30 in appsettings.json and is separate from JWT configuration. Refresh tokens use random bytes and persist only SHA-256 hashes; they have no signing key. AddRefreshTokens is applied locally and the complete flow has passed its SQL integration tests.

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
- [API](docs/api.md)
- [Infrastructure Layer](docs/infrastructure.md)
- [Domain Model](docs/domain.md)

These documents evolve with the implementation and act as the current source of truth for architectural and domain decisions.
