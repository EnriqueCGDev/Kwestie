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
    ├── CreateKwestieAccessDeniedException.cs
    ├── CreateKwestieHandler.cs
    └── CreateKwestieResult.cs
Workspaces/
├── IWorkspaceRepository.cs
├── Create/
│   ├── CreateWorkspaceCommand.cs
│   ├── CreateWorkspaceHandler.cs
│   └── CreateWorkspaceResult.cs
├── Get/
│   ├── GetWorkspaceQuery.cs
│   └── GetWorkspaceHandler.cs
└── List/
    ├── ListWorkspacesQuery.cs
    ├── ListWorkspacesHandler.cs
    └── WorkspaceSummary.cs
```

## Command

`CreateKwestieCommand` is an immutable record containing `WorkspaceId`, `Title`, nullable `Description`, `Priority`, `CreatedById`, and nullable `CategoryId`. It contains no system-generated ID, number, timestamps, or initial lifecycle state.

## Handler

`CreateKwestieHandler` receives `IKwestieRepository`, `IWorkspaceRepository`, and .NET `TimeProvider` through its constructor. `HandleAsync` performs this flow:

```text
Command
    ↓
Handler
    ├── generates Id with Guid.NewGuid()
    ├── obtains CreatedAt from TimeProvider.GetUtcNow()
    ├── constructs the Domain Kwestie
    ├── awaits IWorkspaceRepository.HasActiveMembershipAsync
    ├── rejects access if the workspace or active creator membership is missing
    ├── awaits IKwestieRepository.AddAsync
    └── returns Result
```

The caller's cancellation token is passed to both the access check and `AddAsync`. `DomainException` propagates unchanged; when construction fails, neither repository is called. For valid Domain input, the handler waits for the access check before persisting. Both Admin and Member can create Kwesties when their membership is active. Missing workspaces, users outside the workspace, and inactive memberships all raise `CreateKwestieAccessDeniedException`, an Application exception distinct from Domain invariants, without calling `IKwestieRepository.AddAsync`. Query failures propagate without persisting.

## Result

`CreateKwestieResult` is an immutable record containing only `Id`, matching the entity sent to the repository. It does not return `Number` or `Key` and does not produce a visible reference. Infrastructure's real SQL Server repository test verifies generated-number assignment against the existing local database with InitialCreate applied. The entity's number remains `0` in Application tests using the repository fake.

API registers CreateKwestieHandler as scoped and exposes protected POST /api/workspaces/{workspaceId}/kwesties. The controller supplies WorkspaceId from the route, CreatedById from the validated JWT sub, and CategoryId = null. HTTP DTOs remain in API; it maps the result to `{ kwestieId }` with 201 and no Location, DomainException to 400 ProblemDetails, and CreateKwestieAccessDeniedException to a uniform 403 without details. Application behavior and its membership check are unchanged.

## Application Abstractions

`IKwestieRepository` lives in Application and represents the current use case's external dependency. Its only operation is `Task AddAsync(Kwestie kwestie, CancellationToken cancellationToken = default)`. Infrastructure implements it with EF Core, including `SaveChangesAsync` within `AddAsync`. Application has no separate saving or unit-of-work abstraction.

`IWorkspaceRepository.HasActiveMembershipAsync(workspaceId, userId, cancellationToken)` returns true only for an active membership in an existing workspace, regardless of role. Infrastructure uses a single read-only AnyAsync over WorkspaceMembers, matching both identifiers and IsActive; the workspace FK guarantees existence without another query.

## Time

The handler uses .NET `TimeProvider` rather than reading the real clock directly. Tests supply a small subclass returning a fixed timestamp; no custom clock interface or additional package is needed.

## Create Workspace

`CreateWorkspaceCommand` contains only `Name` and `UserId`. `CreateWorkspaceHandler` generates the workspace ID with `Guid.NewGuid()` and obtains one timestamp through `TimeProvider.GetUtcNow()`. It constructs a `Workspace` and an active `WorkspaceMember` for that user with role `Admin`, the same WorkspaceId, and `JoinedAt` equal to `CreatedAt`.

The handler awaits `IWorkspaceRepository.AddAsync(workspace, initialMember, cancellationToken)` before returning `CreateWorkspaceResult`, which contains `WorkspaceId`. This Application-owned contract requires the workspace and initial membership to be persisted atomically, with completion only after both are saved. Infrastructure implements it with the scoped WorkspaceRepository, one DbContext, and a single awaited SaveChangesAsync using EF's normal transaction. No separate saving or UnitOfWork abstraction is introduced. AddWorkspaces was applied manually to the local Kwestie database. API registers the handler and exposes POST /api/workspaces, supplying UserId exclusively from the validated JWT `sub`.

Intrinsic validation remains in Domain. `DomainException` propagates unchanged from Application, and invalid names or empty user IDs cause no persistence call. API maps Domain rejections to HTTP 400 and handles JWT authentication; Application has no HTTP or JWT dependency. Account existence is not queried by the handler; Infrastructure's membership FK enforces the persisted user reference.

## List Workspaces

`ListWorkspacesQuery` contains UserId, supplied by API from the validated JWT. `ListWorkspacesHandler` awaits the existing repository's `ListForUserAsync`, forwards cancellation, and maps the Domain workspaces to Application-owned `WorkspaceSummary` records containing WorkspaceId, Name, and CreatedAt. No separate query abstraction or mediator is introduced.

The repository contract returns only workspaces where that user has an active membership, ordered by CreatedAt ascending and WorkspaceId ascending as a tie-breaker. Ordering is query behavior, not a Domain rule. API registers the handler and exposes GET /api/workspaces; HTTP DTOs remain in API. Unit tests verify user/cancellation forwarding, result transformation, and awaiting the asynchronous read.

## Get Workspace

`GetWorkspaceQuery` contains WorkspaceId and UserId. `GetWorkspaceHandler` awaits `IWorkspaceRepository.GetForUserAsync`, forwards both identifiers and CancellationToken, and reuses the existing WorkspaceSummary result. An unavailable workspace is represented by null, without an access exception or a second membership check. Application has no HTTP, JWT, EF, or Infrastructure dependency.

The repository returns a workspace only when the requested user has an active membership in it, regardless of Admin/Member role. API registers the handler as scoped and exposes GET /api/workspaces/{workspaceId}, using the route ID and validated JWT sub. A summary becomes WorkspaceResponse with 200; null becomes a uniform empty 404. Unit tests cover forwarding, mapping, absence, and awaiting the repository. HTTP/SQL tests cover both active roles, isolation, indistinguishable unavailable responses, malformed route binding, and no tracking. Angular navigation remains pending.

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

Rotation commits before access-token generation. If generation subsequently fails, the error propagates and the consumed token stays revoked; there is no rollback across these two contracts or automatic retry. Login persists its refresh token after generating the access token and returns only after saving succeeds. API exposes session renewal through POST /api/auth/refresh and registers its handler in the composition root. Angular authentication UI and initial session recovery are implemented; automatic refresh after a 401 remains pending.

## Logout

LogoutSessionCommand contains only the nullable refresh token. LogoutSessionHandler awaits IRefreshTokenService.RevokeAsync, forwarding cancellation and returning no result data. The existing token service contract is extended instead of introducing a separate abstraction. Revocation is idempotent and reveals no token state. API registers the handler and exposes POST /api/auth/logout; Application has no HTTP or cookie dependencies.

## Validation Boundaries

Domain enforces intrinsic rules using the entity's state and input data. Application leaves those checks to the constructor without duplicating, catching, or translating them.

Checks requiring other data or coordination belong to Application orchestration. Create Kwestie requires an existing workspace and an active creator membership. Category/workspace compatibility is not checked because Categories are not implemented. The current command retains optional CategoryId and its Domain invariant; the implemented HTTP create contract excludes CategoryId and supplies null until Categories are implemented.

## Current Implementation Scope

Implemented: `CreateKwestieCommand`, `CreateKwestieHandler`, `CreateKwestieResult`, `IKwestieRepository`, and feature tests in `Kwestie.Application.Tests/Kwesties/Create`.

Create Workspace is implemented through `CreateWorkspaceCommand`, `CreateWorkspaceHandler`, `CreateWorkspaceResult`, and `IWorkspaceRepository`. Tests use a recording fake and fixed TimeProvider to verify the Workspace/Admin membership pair, shared identifiers/timestamps, cancellation forwarding, waiting for persistence, persistence failure propagation, and Domain rejection without a repository call. Concrete persistence and repository DI registration are implemented in Infrastructure; AddWorkspaces was applied manually and Workspace SQL persistence/atomicity is validated. Create/List/Get handlers are registered in API and exposed through protected POST/GET /api/workspaces and GET /api/workspaces/{workspaceId}, with listing/creation UI implemented and Angular navigation still pending. The protected Create Kwestie endpoint is also implemented; membership management remains pending.

Register is also implemented through RegisterUserCommand, RegisterUserHandler, RegisterUserResult, and IUserRegistration. Unit tests use a small fake to verify input/cancellation forwarding and success/error results; a separate Infrastructure integration test verifies real Identity user persistence.

Logout unit tests verify token/cancellation forwarding and waiting for revocation. Login and Refresh unit tests use small fakes to verify input/cancellation forwarding, exact user IDs, both tokens and expirations, and no generation on rejection. Result tests reject empty IDs, blank tokens, and invalid UTC expiration values. IntegrationTests validates credential checking and the full Login + Refresh + JWT flow against SQL Server with cleanup in finally. With 20260929220522_AddRefreshTokens applied locally, SQL tests have passed for issuance, hash-only persistence, rotation, reuse rejection, expiration, and concurrency. The latest full suite passed on 2026-10-08 with AddWorkspaces applied, individual Workspace access, and Create Kwestie HTTP/SQL coverage: 174 tests, 174 passed, 0 failed, 0 skipped. Database-free tests cover JWT, mapping, DI, configuration, and malformed refresh rejection.

Create Kwestie tests use local recording repository fakes and a fixed time provider. They cover the created entity and result, generated ID, timestamps, unassigned number, cancellation forwarding to both repositories, active Admin/Member access, each access rejection without persistence, awaiting validation and saving, query failure propagation, and Domain rejection without a repository call.

Infrastructure provides persistence and generated-number mapping, with context and repository DI registration, InitialCreate, and a verified real repository round trip against the existing local database. Workspace/active-membership validation and the Create Kwestie API endpoint are implemented. HTTP/SQL tests cover authentication, uniform access rejection, closed request contracts, and persistence for both active roles, including SQL-generated Number. Visible references, Categories and their compatibility checks, GET/list Kwesties, and Kwestie UI remain pending.
