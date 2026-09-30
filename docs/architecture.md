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

JWT access-token generation implements Application's IAccessTokenGenerator in Infrastructure. A separate AddJwtAuthentication registration supplies validated configuration, token generation, and Bearer validation for the API composition root.

Refresh-token generation, hashing, and persistence also belong to Infrastructure, behind Application's IRefreshTokenService. The implementation is validated against SQL Server, and 20260929220522_AddRefreshTokens is applied locally.

Planned responsibilities include:

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

Identity is implemented with AddIdentityCore<ApplicationUser> and EF stores. Register and Login use IUserRegistration and IUserAuthentication, with Infrastructure adapters using UserManager. Registration uses UserName = Email and Identity requires unique email while retaining default password policies. IUserAuthentication returns only credential-validation data. After successful authentication, LoginUserHandler generates an access token and persists a refresh token through IAccessTokenGenerator and IRefreshTokenService. It returns UserId, AccessToken, AccessTokenExpiresAtUtc, RefreshToken, and RefreshTokenExpiresAtUtc. Unknown email and incorrect password return the same failure with all token fields null and no token generation.

Infrastructure issues HS256 JWT access tokens and configures Bearer validation through AddJwtAuthentication. API explicitly invokes that registration, using shared settings for signature, issuer, audience, and lifetime validation, and calls UseAuthentication before UseAuthorization. The sub claim is preserved without inbound mapping. AddRefreshTokens is a separate registration with a configurable lifetime. Application references no Identity, JWT, cryptography, EF, or Infrastructure types.

RefreshSessionHandler rotates the supplied token through IRefreshTokenService and generates a new access token for the user recovered from persistence, never a user ID supplied by the caller. Infrastructure stores only SHA-256 hashes of random refresh tokens, uses rowversion to prevent concurrent reuse, and saves revocation plus replacement atomically. Domain is unchanged. Missing, malformed, expired, revoked, and concurrently consumed tokens have the same public failure result. API exposes POST /api/auth/register, POST /api/auth/login, POST /api/auth/refresh, and POST /api/auth/logout. External providers remain unimplemented.

The context uses IdentityUserContext<ApplicationUser, Guid> without global roles. Future Workspace Admin/Member roles are separate domain concepts, not global Identity roles. No roles are registered or seeded. AddIdentity was applied manually locally. 20260929220522_AddRefreshTokens is also applied locally, and the complete Login + Refresh + JWT flow has passed real SQL tests. Angular authentication integration is still absent, so authentication is not complete. CORS is not configured.

OAuth 2.0 / OpenID Connect may be introduced later if Kwestie needs external identity providers, enterprise SSO, or third-party clients.

## Persistence

SQL Server through Entity Framework Core is the accepted persistence direction.

The persistence implementation and `InitialCreate` migration are present. The migration was applied locally to the existing `Kwestie` database. A real repository round-trip test verifies insertion, SQL Server IDENTITY generation, EF's update of `Number`, and retrieval through a separate DbContext.

`AddIdentity` also exists and was applied manually to the same database. AspNetUsers, AspNetUserClaims, AspNetUserLogins, and AspNetUserTokens now exist physically in SQL Server, and the migration is recorded in `__EFMigrationsHistory`.

AddRefreshTokens adds only the Infrastructure RefreshTokens table with a cascading FK to AspNetUsers, a unique hash index, and rowversion. 20260929220522_AddRefreshTokens is applied to the local Kwestie database. Older migrations, Domain, and Kwesties mapping remain unchanged.

Persistence configuration belongs in Infrastructure.

Domain must not contain EF Core attributes or persistence-specific dependencies.

EF Core Fluent API configures the current entity. Migrations belong to Infrastructure and are applied manually; tests do not create databases or apply migrations. See [Infrastructure Layer](infrastructure.md) for configuration and scope.

## API Style

Kwestie will use pragmatic REST.

Normal resource operations may use standard REST endpoints, while explicit domain actions may use action-oriented endpoints when that better represents the use case.

AuthenticationController adapts HTTP requests to Application commands and maps results to API-owned DTOs. Register, Login, Refresh, and Logout handlers are registered as scoped services in Program.cs. The controller owns refresh-cookie handling; Application and Infrastructure have no cookie responsibilities. Access tokens are returned as JSON for Bearer use. Raw refresh tokens appear only in an HttpOnly, Secure, SameSite=Strict cookie scoped to /api/auth, with no Domain and the returned refresh expiration. Failed refresh deletes that cookie and returns a generic 401. Anonymous Logout revokes only the supplied active refresh token and deletes the same cookie, returning 204 without revealing token state; it does not require a valid access token. See [API](api.md) for the implemented contracts.

## Testing

The solution currently contains:

- `Kwestie.Domain.Tests`
- `Kwestie.Application.Tests`
- `Kwestie.IntegrationTests`

At the current stage, meaningful automated coverage exists in all three test projects.

Application tests cover Create Kwestie using a small repository fake and a controlled TimeProvider, and Register, Login, and Refresh using small service fakes without mocking libraries. They verify both tokens/expirations, exact user IDs, cancellation forwarding, result invariants, and no issuance on invalid credentials or invalid refresh.

IntegrationTests contains:

- HTTP pipeline tests through WebApplicationFactory with real DI and SQL Server: registration, duplicate rejection, invalid login, JWT responses, secure refresh cookies, successive rotation, reuse rejection, and idempotent Logout with real revocation, cookie deletion, and an expired access token. HTTPS clients keep Secure enabled; test JWT configuration is public and separate from developer secrets.
- EF model and materialization checks that do not require a database.
- A real `KwestieRepository` round-trip test against SQL Server, including generated `Number` and retrieval through a separate DbContext.
- A real user-registration test against SQL Server through the `IUserRegistration` implementation and Identity's `UserManager`. It verifies persistence in `AspNetUsers`, retrieval through a separate DbContext, normalized Email/UserName values, an Identity-generated `PasswordHash`, password validation through Identity's password hasher, and duplicate-email rejection.
- A real Login test against SQL Server that registers a user, validates credentials through `IUserAuthentication` in a fresh scope, checks equivalent rejection results for an incorrect password and an unknown email, and verifies user cleanup in finally. It checks that EF has no pending model changes before writing.
- Database-free JWT tests for claims, signature, configured lifetime, Bearer validation, invalid tokens, and startup options validation using only test signing material.
- Database-free refresh tests for mapping, FK/cascade, unique hash index, rowversion, absence of raw storage, DI, options, and malformed input rejection.
- Passing SQL refresh tests for issuance, hash-only persistence, rotation, reuse rejection, expiration, concurrent consumption, and the complete Login + Refresh + JWT flow, with user/token cleanup in finally.

The full real test suite requires the local Kwestie database with InitialCreate, AddIdentity, and AddRefreshTokens applied, plus ConnectionStrings:Kwestie in shared API User Secrets. All three migrations are applied locally. Tests remove their own data in finally and never create the database or apply migrations. The full suite passed on 2026-09-30, including real HTTP and SQL coverage: 106 tests, 106 passed, 0 failed, 0 skipped. Model/snapshot agreement is checked separately from database migration application.

Domain and current Application tests run without database, API, or infrastructure dependencies.

## C# Conventions

These are project coding conventions, not Clean Architecture rules:

- Simple services receiving dependencies through DI use primary constructors.
- Dependencies used by instance methods are stored in private `readonly` fields; methods use those fields exclusively, not the primary constructor parameters.
- Parameters used only for base-constructor chaining do not require a redundant field.
- Classic constructors remain appropriate for invariants, normalization, multiple construction paths, or significant initialization logic.

## Working Agreement

When implementing a feature:

- Respect the documented architecture.
- Do not silently change an existing architectural decision.
- Do not introduce new dependencies or patterns without a concrete need.
- If a documented decision appears to need revision, explain the reason before implementing the change.
- Keep changes focused on the requested scope.
- Keep documentation aligned with the implementation.
