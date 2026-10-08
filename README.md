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
- Refresh and idempotent Logout endpoints with secure refresh-token cookie handling
- HTTP integration coverage using the real API and SQL Server

### Planned

- Workspace selection/navigation, dashboard, and automatic refresh after a 401
- CORS/frontend configuration according to deployment
- Docker
- CI/CD

## Current Status

Kwestie is under active development.

InitialCreate, AddIdentity, 20260929220522_AddRefreshTokens, and 20261007152213_AddWorkspaces are applied to the local Kwestie database. Login returns access and refresh tokens with separate UTC expirations, and Refresh rotates the persisted token. The complete Login + Refresh + JWT flow has been validated against SQL Server, including issuance, hash-only persistence, rotation, reuse rejection, expiration, and concurrency. API exposes Register, Login, Refresh, and Logout endpoints. Access tokens are returned as JSON for Bearer use; refresh tokens are sent only in a secure HttpOnly cookie. Angular has Login and Register screens backed by the in-memory authentication service. It attempts one cookie-backed Refresh during startup and attaches the in-memory access token to Kwestie API requests outside `/api/auth/...`. Login navigates to the guarded `/app` route, which lists the user's Workspaces, supports creation and Logout, and reloads the list after creation. Workspace selection/navigation, a functional dashboard, automatic refresh after a 401, and deployment-specific CORS remain pending. Authentication is not complete.

Implemented so far:

- Base Clean Architecture solution
- Angular Login and Register screens, guarded `/app` Workspace listing/creation screen, and in-memory authentication service
- Domain and test projects
- Initial `Kwestie` entity
- `Open -> InProgress -> Resolved -> Closed` lifecycle
- Unit tests for the current domain behavior
- Create Kwestie application use case and its unit tests
- Workspace/WorkspaceMember models, Create Workspace with initial Admin membership, EF SQL Server mappings, scoped repository, and persistence tests; AddWorkspaces is applied manually locally and real SQL persistence/atomicity is validated; protected POST/GET /api/workspaces are implemented; listing/creation UI is implemented; Workspace navigation and membership management remain pending

Create Kwestie has an Infrastructure repository implementation using EF Core and SQL Server. `InitialCreate` exists and was applied locally to the existing `Kwestie` database. A real repository round-trip test verifies insertion, generated Number, retrieval, and cleanup. There is still no Create Kwestie API endpoint; workspace and membership checks required before exposing this use case are not implemented.

Full authentication is not implemented yet. Workspace navigation, automatic refresh after a 401, assignment, comments, history, search, and dashboard functionality remain pending.

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

Domain and Application tests require no SQL Server or Infrastructure. IntegrationTests includes database-free EF, JWT, and refresh mapping/configuration checks, alongside real SQL tests. Full dotnet test requires InitialCreate, AddIdentity, AddRefreshTokens, and AddWorkspaces applied to the local Kwestie database and ConnectionStrings:Kwestie in shared API User Secrets. All four migrations are applied manually locally. The full suite passed on 2026-10-07: 149 tests, 149 passed, 0 failed, 0 skipped, including real Workspace persistence/atomicity, protected Workspace HTTP isolation, and authentication HTTP/SQL tests. Tests never create the database or apply migrations; they clean up only their own data in finally.

Workspace mappings, materialization, DI, and repository save/cancellation behavior pass database-free checks. Real Workspace persistence and atomicity also passed against the local SQL Server database with AddWorkspaces applied. HasPendingModelChanges() is false. See [Infrastructure validation](docs/infrastructure.md#migrations) for the current results.

API and IntegrationTests use the same `UserSecretsId`; do not store the connection string or passwords in the repository. The development SQL Server currently runs in Docker, independently of any application Docker configuration in this repository. `dotnet build` does not require SQL Server. See [Infrastructure configuration](docs/infrastructure.md#configuration).

JWT tests use separate public test configuration and do not need the developer's signing key. They verify generation, cryptographic/Bearer validation, rejected tokens, and startup settings. SQL tests have passed for refresh issuance, hash-only persistence, rotation, reuse rejection, expiration, concurrent consumption, and Login + Refresh + JWT. The RefreshTokens table exists locally following application of 20260929220522_AddRefreshTokens.

To start the API locally, also configure Jwt:Key through User Secrets; issuer, audience, and the default 15-minute lifetime are in appsettings.json. No signing key is stored in the repository. See the command and requirements in [JWT configuration](docs/infrastructure.md#jwt-configuration).

RefreshTokens:LifetimeDays defaults to 30 in appsettings.json and is separate from JWT configuration. Refresh tokens use random bytes and persist only SHA-256 hashes; they have no signing key. AddRefreshTokens is applied locally and the complete flow has passed its SQL integration tests.

## Frontend development

From `src/frontend/kwestie-web`, run `npm start` and open `https://localhost:4200` (accept the local development certificate). The development proxy forwards `/api/**` to `https://localhost:7204`; start the HTTPS API separately. `secure: false` in the proxy accepts the backend development certificate only; it does not disable HTTPS or the cookie's Secure attribute. Services use relative URLs, without CORS or production deployment configuration.

Run `npm run build` and `npm test -- --watch=false` for frontend validation. AuthService returns Observables: callers subscribe to execute requests. Session state lives only in memory; reloading attempts one `/api/auth/refresh` with the HttpOnly cookie. Failure leaves the app running without a session. The Bearer interceptor does not refresh or retry requests.

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
