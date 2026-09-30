# Application Layer

## Responsibility

Application orchestrates use cases, depends on Domain, and defines the abstractions it needs from external systems. It does not implement infrastructure or duplicate intrinsic rules already enforced by Domain.

## Feature Organization

The implemented features are organized as follows:

```text
Authentication/
├── Login/
│   ├── IUserAuthentication.cs
│   ├── LoginUserCommand.cs
│   ├── LoginUserHandler.cs
│   ├── LoginUserResult.cs
│   └── UserAuthenticationResult.cs
├── Logout/
│   ├── LogoutSessionCommand.cs
│   └── LogoutSessionHandler.cs
├── Refresh/
│   ├── RefreshSessionCommand.cs
│   ├── RefreshSessionHandler.cs
│   └── RefreshSessionResult.cs
├── Register/
│   ├── IUserRegistration.cs
│   ├── RegisterUserCommand.cs
│   ├── RegisterUserHandler.cs
│   └── RegisterUserResult.cs
└── Tokens/
    ├── IAccessTokenGenerator.cs
    ├── AccessTokenResult.cs
    ├── IRefreshTokenService.cs
    ├── RefreshTokenResult.cs
    └── RefreshTokenRotationResult.cs
Kwesties/
├── IKwestieRepository.cs
└── Create/
    ├── CreateKwestieCommand.cs
    ├── CreateKwestieHandler.cs
    └── CreateKwestieResult.cs
```

## Command

`CreateKwestieCommand` is an immutable record containing `WorkspaceId`, `Title`, nullable `Description`, `Priority`, `CreatedById`, and nullable `CategoryId`. It contains no system-generated ID, number, timestamps, or initial lifecycle state.

## Handler

`CreateKwestieHandler` receives `IKwestieRepository` and .NET `TimeProvider` through its constructor. `HandleAsync` performs this flow:

```text
Command
    ↓
Handler
    ├── generates Id with Guid.NewGuid()
    ├── obtains CreatedAt from TimeProvider.GetUtcNow()
    ├── constructs the Domain Kwestie
    ├── awaits IKwestieRepository.AddAsync
    └── returns Result
```

The caller's cancellation token is passed to `AddAsync`. `DomainException` propagates unchanged; when construction fails, the repository is not called.

## Result

`CreateKwestieResult` is an immutable record containing only `Id`, matching the entity sent to the repository. It does not return `Number` or `Key` and does not produce a visible reference. Infrastructure's real SQL Server repository test verifies generated-number assignment against the existing local database with InitialCreate applied. The entity's number remains `0` in Application tests using the repository fake.

## Application Abstractions

`IKwestieRepository` lives in Application and represents the current use case's external dependency. Its only operation is `Task AddAsync(Kwestie kwestie, CancellationToken cancellationToken = default)`. Infrastructure implements it with EF Core, including `SaveChangesAsync` within `AddAsync`. Application has no separate saving or unit-of-work abstraction.

## Time

The handler uses .NET `TimeProvider` rather than reading the real clock directly. Tests supply a small subclass returning a fixed timestamp; no custom clock interface or additional package is needed.

## Register

`RegisterUserCommand` contains only Email and Password. `RegisterUserHandler` forwards both values and the CancellationToken to the feature-specific `IUserRegistration.RegisterAsync` contract. Application does not reference UserManager, ApplicationUser, IdentityResult, or other Identity types, and does not hash passwords or write users through EF.

The current account convention is UserName = Email. Infrastructure implements registration through Identity. `RegisterUserResult` reports `Succeeded`, a nullable Guid `UserId`, and a read-only collection of error strings. Success includes the user ID and no errors; rejection includes error descriptions and no user ID. Infrastructure exceptions are not converted into validation failures.

Cancellation is forwarded to Infrastructure, which checks it before invoking Identity. UserManager.CreateAsync has no CancellationToken overload, so cancellation cannot interrupt that operation through this API. Register does not emit tokens. API exposes the use case through POST /api/auth/register and registers its handler in the composition root.

## Login

`LoginUserCommand` contains only Email and Password. `LoginUserHandler` first delegates both values and the CancellationToken to `IUserAuthentication.AuthenticateAsync`. This contract returns `UserAuthenticationResult`, containing only credential-validation success and a nullable user ID. Infrastructure does not construct the final Login result.

If credentials are valid, the handler calls `IAccessTokenGenerator.Generate` and then awaits `IRefreshTokenService.IssueAsync` for exactly the authenticated user ID, forwarding cancellation. `LoginUserResult` contains Succeeded, UserId, AccessToken, AccessTokenExpiresAtUtc, RefreshToken, and RefreshTokenExpiresAtUtc. Success requires a non-empty user ID, nonblank tokens, and non-default UTC expirations. Dependencies use primary-constructor parameters stored in private readonly fields. Application remains independent of Identity, JWT, cryptography, EF, and Infrastructure types.

An unknown email and an incorrect password both return Succeeded = false, with null UserId and all four token/expiration fields null. Neither token generator nor refresh-token service is called. Invalid credentials are results, not exceptions; infrastructure failures still propagate. This is equivalence of returned data, not a constant-time execution guarantee.

Infrastructure checks cancellation before the lookup and before password validation; the UserManager operations do not accept the caller's CancellationToken. API exposes Login through POST /api/auth/login and registers its handler in the composition root. Application remains independent of HTTP transport. Its persistence flow has been validated against local SQL Server with 20260929220522_AddRefreshTokens applied.

## Access Tokens

`Authentication/Tokens/IAccessTokenGenerator` provides synchronous `AccessTokenResult Generate(Guid userId)`. Token creation is local computation; its Application-owned result contains only the token string and UTC expiration. Login and Refresh both use this contract. Infrastructure implements it with JWT; Application does not reference JWT token types, signing credentials, or validation libraries.

## Refresh Tokens and Session Renewal

`IRefreshTokenService` exposes IssueAsync(Guid userId, CancellationToken) and RotateAsync(string? refreshToken, CancellationToken). Issue returns RefreshTokenResult with the newly generated public token and its UTC expiration. Successful rotation returns RefreshTokenRotationResult containing the persisted UserId, a new refresh token, and its expiration. Invalid, malformed, expired, revoked, previously used tokens, and a lost consumption race all return the same failure with null fields. Hashing, random generation, storage, and concurrency belong exclusively to Infrastructure.

`RefreshSessionCommand` contains only RefreshToken, never a client-supplied UserId. RefreshSessionHandler awaits rotation, forwarding cancellation. On failure it does not generate an access token. On success it uses exactly the UserId returned by rotation to generate a new access token. RefreshSessionResult has the same six fields and success invariants as LoginUserResult; an invalid refresh has no user, tokens, or expirations. The previous refresh token is never returned for reuse.

Rotation commits before access-token generation. If generation subsequently fails, the error propagates and the consumed token stays revoked; there is no rollback across these two contracts or automatic retry. Login persists its refresh token after generating the access token and returns only after saving succeeds. API exposes session renewal through POST /api/auth/refresh and registers its handler in the composition root. Angular authentication UI and automatic session recovery remain pending; the base HTTP service infrastructure is implemented.

## Logout

LogoutSessionCommand contains only the nullable refresh token. LogoutSessionHandler awaits IRefreshTokenService.RevokeAsync, forwarding cancellation and returning no result data. The existing token service contract is extended instead of introducing a separate abstraction. Revocation is idempotent and reveals no token state. API registers the handler and exposes POST /api/auth/logout; Application has no HTTP or cookie dependencies.

## Validation Boundaries

Domain enforces intrinsic rules using the entity's state and input data. Application leaves those checks to the constructor without duplicating, catching, or translating them.

Checks requiring other data or coordination belong to Application orchestration. Workspace existence, creator membership, category/workspace compatibility, active membership, and authorization are not implemented. Create Kwestie must not be exposed through an API endpoint until the required workspace and membership checks exist.

## Current Implementation Scope

Implemented: `CreateKwestieCommand`, `CreateKwestieHandler`, `CreateKwestieResult`, `IKwestieRepository`, and feature tests in `Kwestie.Application.Tests/Kwesties/Create`.

Register is also implemented through RegisterUserCommand, RegisterUserHandler, RegisterUserResult, and IUserRegistration. Unit tests use a small fake to verify input/cancellation forwarding and success/error results; a separate Infrastructure integration test verifies real Identity user persistence.

Logout unit tests verify token/cancellation forwarding and waiting for revocation. Login and Refresh unit tests use small fakes to verify input/cancellation forwarding, exact user IDs, both tokens and expirations, and no generation on rejection. Result tests reject empty IDs, blank tokens, and invalid UTC expiration values. IntegrationTests validates credential checking and the full Login + Refresh + JWT flow against SQL Server with cleanup in finally. With 20260929220522_AddRefreshTokens applied locally, SQL tests have passed for issuance, hash-only persistence, rotation, reuse rejection, expiration, and concurrency. The full suite passed on 2026-09-30: 106 tests, 106 passed, 0 failed, 0 skipped. Database-free tests cover JWT, mapping, DI, configuration, and malformed refresh rejection.

Tests use a local recording repository fake and a fixed time provider. They cover the created entity and result, generated ID, timestamps, unassigned number, cancellation-token forwarding, waiting for the repository, and domain rejection without a repository call.

Infrastructure provides persistence and generated-number mapping, with context and repository DI registration, InitialCreate, and a verified real repository round trip against the existing local database. Visible references, cross-entity checks, authorization, handler registration, and an API endpoint remain pending.
