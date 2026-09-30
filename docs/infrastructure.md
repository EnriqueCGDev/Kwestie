# Infrastructure Layer

## Responsibility

Infrastructure implements technical details required by Application and depends on Application and Domain. Domain contains no EF Core references or persistence attributes.

## Persistence

EF Core 10 and its SQL Server provider are configured through `KwestieDbContext`. The context receives typed options and exposes `Kwesties`. `KwestieConfiguration` applies Fluent API mapping. The API registers the context and scoped repository through `AddInfrastructure(connectionString)`; registration does not create or connect to the database.

## Identity

ASP.NET Core Identity is integrated exclusively in Infrastructure. `Identity/ApplicationUser` derives from `IdentityUser<Guid>` without additional properties. Domain and Application do not depend on Identity types.

`KwestieDbContext` derives from `IdentityUserContext<ApplicationUser, Guid>`, sharing the existing SQL Server `Kwestie` database. It calls the base model configuration before applying `KwestieConfiguration` and retains its `Kwesties` DbSet. `CreatedById` and `AssignedToId` remain Guid references without navigations or foreign keys to ApplicationUser.

`AddInfrastructure` registers Identity Core with EF stores and sets `options.User.RequireUniqueEmail = true`. Default password policies remain unchanged. There are no global role services or role tables in this model; future Workspace Admin/Member roles are unrelated to global Identity roles.

AspNetUsers, AspNetUserClaims, AspNetUserLogins, and AspNetUserTokens are mapped in the model and exist physically in the local SQL Server Kwestie database after the manual application of AddIdentity. AspNetUserTokens is not used for application refresh tokens; those use the new RefreshTokens model below. No authentication cookies, external providers, default token providers, or new endpoints are registered. Email confirmation and password recovery remain unimplemented.

### User registration

`Identity/UserRegistration` implements Application's `IUserRegistration` using `UserManager<ApplicationUser>`. It generates a Guid, sets Email and UserName to the submitted email, and calls `CreateAsync(user, password)`. Identity validates the input and password, hashes the password, normalizes email/username, and persists the user in AspNetUsers. The adapter neither assigns PasswordHash nor writes directly through the DbContext. Identity error descriptions become Application-owned strings in RegisterUserResult.

The adapter is scoped in AddInfrastructure; Application handlers are not registered there. Cancellation is checked before CreateAsync, which does not accept a CancellationToken. Register returns only a user ID or errors, without tokens or an HTTP endpoint.

A real SQL Server test registers a unique email, reads the user through a separate context, checks normalized values and the stored hash, verifies the password through Identity's PasswordHasher, and confirms rejection of the same email. Its finally block removes only users with that test's unique email and verifies cleanup. No credentials or hashes are logged.

Database-free tests verify Guid user keys, Identity user tables, the absence of roles and Kwestie-to-user foreign keys, and resolution of UserManager with an EF user store sharing the registered context.

### User authentication

`Identity/UserAuthentication` implements Application's `IUserAuthentication` and is registered as scoped in `AddInfrastructure`. It uses `UserManager<ApplicationUser>.FindByEmailAsync` and `CheckPasswordAsync`; it does not query users through DbContext, read or compare PasswordHash manually, or invoke PasswordHasher directly. Its primary constructor initializes a private readonly UserManager field.

A valid password returns the user's Guid in Application's `UserAuthenticationResult`. An unknown email and an incorrect password return the same public result: `Succeeded = false`, `UserId = null`. The adapter exposes no reason-specific errors and logs no credentials. This does not guarantee identical execution timing. This adapter only validates credentials; LoginUserHandler separately requests an access token and constructs the final Login result.

Existing Identity policies remain unchanged. CheckPasswordAsync does not increment failed-access counts or enforce lockout; no additional lockout is implemented. Identity may upgrade an outdated password hash on successful verification. Cancellation is checked before the lookup and before password validation, since these UserManager methods have no CancellationToken parameter.

A real SQL Server test creates a unique user through the existing Register adapter, validates correct credentials from a fresh scope, and verifies equivalent rejection results for a wrong password and a missing email. It deletes only its test user in finally and verifies removal. The test checks for pending EF model changes before any write. Credential validation alone changes no database model; refresh-token persistence uses the applied AddRefreshTokens migration.

## JWT Access Tokens

`Authentication/JwtAccessTokenGenerator` implements Application's `IAccessTokenGenerator` using Microsoft IdentityModel and HMAC SHA-256 (HS256). Its only application claims are `sub` (the user's Guid) and `jti` (a new Guid for each token). Standard `iss`, `aud`, `iat`, `nbf`, and `exp` fields supply issuer, audience, issue/valid time, and expiration. Tokens contain no email, roles, workspace, credentials, or configuration secrets.

The generator uses TimeProvider and rounds UTC time to whole seconds so the returned ExpiresAtUtc exactly matches the encoded expiration. `AddJwtAuthentication(configuration)` separately registers the generator, validated JwtOptions, and JWT Bearer authentication. It uses TryAddSingleton for TimeProvider.System so a caller-provided clock is preserved. AddInfrastructure(connectionString) remains unchanged and requires no JWT configuration.

API explicitly calls AddJwtAuthentication and runs UseAuthentication before UseAuthorization. Bearer validation uses the same JwtOptions as issuance and requires a signature with HS256, the configured issuer/audience, and a valid lifetime with zero clock skew. MapInboundClaims is false and NameClaimType is sub. Tokens are not saved in authentication properties and detailed authentication errors are not exposed. No new endpoints or Authorize attributes are added.

Tests validate generation, the registered Bearer handler, claims, lifetime, altered signatures, wrong signing keys, wrong issuer/audience, expired/future tokens, and startup configuration failures. The SQL Server Login test includes refresh issuance and rotation; this Login + Refresh + JWT flow has passed against local SQL Server with AddRefreshTokens applied. JWT test configuration contains only explicitly labelled public test material; developer signing secrets are not used.

## Refresh Tokens

RefreshToken is an Infrastructure persistence model, not a Domain entity. Fluent API maps RefreshTokens with Guid Id (PK), Guid UserId, TokenHash, CreatedAtUtc, ExpiresAtUtc, nullable RevokedAtUtc, and SQL Server RowVersion. TokenHash is required char(64), non-Unicode, fixed length, with a unique index. UserId is indexed and references AspNetUsers with cascade deletion. ApplicationUser has no refresh-token navigation. No raw-token property or column exists.

RefreshTokenService implements Application's IRefreshTokenService. IssueAsync rejects Guid.Empty, obtains UTC time from TimeProvider, generates 32 CSPRNG bytes (256 bits) with RandomNumberGenerator, and encodes them as unpadded Base64Url. It hashes the public token's UTF-8 bytes with SHA-256 and stores the uppercase hexadecimal hash. SaveChangesAsync persists only the hash and metadata; the public token is returned once to the caller, never loaded from SQL or logged.

RotateAsync accepts only the canonical 43-character Base64Url representation of 32 bytes, hashes it, and looks up the hash. Missing, malformed, revoked, or expired tokens produce the same invalid result. Expiration is inclusive: ExpiresAtUtc <= now is invalid. A valid rotation revokes the old row and creates a new token with a full configured lifetime. Both writes use one SaveChangesAsync and EF's transaction, so either both persist or neither does.

RowVersion prevents two requests from consuming the same stored version. A concurrency exception for the consumed row is converted to an invalid result only when that row is now revoked or deleted. The rolled-back replacement and old entry are detached, preventing a later save in the losing context from persisting an orphan replacement. Other failures propagate. No replacement chain, absolute session cap, background cleanup, or logout/manual revocation is implemented. Revoked rows remain stored until their user is deleted or future cleanup is introduced.

Application's Refresh handler generates the access token after rotation commits. A later generation/transport failure cannot recover the consumed token; automatic retries and cross-contract rollback are not implemented in this version.

Database-free tests cover mapping, uniqueness, FK/cascade, concurrency metadata, timestamps, absence of raw storage, DI, options, and malformed inputs. SQL tests have passed for issuance, hash-only persistence, successive rotations, reuse rejection, expiration at/after the boundary, unknown tokens, concurrent consumption, and Login + Refresh + JWT. The concurrency test synchronizes two contexts at SavingChanges after both read the original version, without sleeps, and checks that a later save cannot persist the loser's replacement. All SQL tests clean up only their temporary users and verify cascaded token removal. These SQL tests ran successfully against the local database with 20260929220522_AddRefreshTokens applied.

## Kwestie Persistence

The `Kwesties` table uses the Application-generated Guid `Id` as its primary key, with database generation disabled. `Number` is a SQL Server `bigint IDENTITY(1,1)` with a unique index, global across workspaces. Gaps are permitted. On successful insertion, EF reads the generated number back into the entity; an unpersisted entity starts at zero.

All entity properties are explicitly mapped, including getter-only properties. Required values and nullable values follow the Domain model. Enums retain their numeric representation. Title and Description have no configured maximum length. There are no foreign keys to Workspace, User, or Category, and no visible-reference generation.

EF materialization is verified without changing Domain: a test invokes EF's materializer with stored values and checks all properties, including a closed status, assignment, generated number, and lifecycle timestamps. This verifies constructor binding and property restoration without a database; it does not verify a real SQL Server insert or generated-value round trip.

A separate repository integration test now verifies a real SQL Server round trip. It inserts through `KwestieRepository.AddAsync`, confirms that SQL Server generates a positive IDENTITY number and EF updates `Number` after `SaveChangesAsync`, and checks the unchanged Id and stored data using a separate DbContext. A `finally` block deletes only the test's uniquely identified row and verifies its removal. The test does not assume that numbering starts at 1 on each run; cleanup can leave permitted identity gaps.

## Repository

`KwestieRepository` implements Application's `IKwestieRepository`. `AddAsync` tracks the entity and awaits `SaveChangesAsync` with the caller's cancellation token. There is no separate UnitOfWork because the current use case does not require one.

## Configuration

The API reads `ConnectionStrings:Kwestie` through `IConfiguration` and fails at startup with a clear message if it is missing or blank. Infrastructure receives the string from the composition root; it contains no environment-specific connection values.

Local development uses .NET User Secrets associated with the API's `UserSecretsId`. Configure the `ConnectionStrings:Kwestie` secret locally for the API project; do not store passwords or real connection strings in source control or appsettings files. No secret is supplied by this repository.

IntegrationTests reuses the same `UserSecretsId` and reads `ConnectionStrings:Kwestie` directly from User Secrets. No connection-string value or password is logged or committed. The real test fails with a clear message if the secret is missing; it requires the existing local database and applied migrations, and is not skipped automatically.

The current local SQL Server runs in Docker. The local server is `localhost,1433` and the existing database is `Kwestie`. These are local setup details, not hardcoded Infrastructure defaults or repository-owned Docker configuration.

### JWT configuration

The API requires Jwt:Issuer, Jwt:Audience, Jwt:AccessTokenMinutes, and Jwt:Key. Issuer and audience must be nonblank, lifetime must be positive, and the nonblank key must contain at least 32 bytes when UTF-8 encoded. The key is used as UTF-8 text, not decoded as Base64. ValidateOnStart rejects missing or invalid settings with configuration names, without disclosing the key. The generator and Bearer validator share the same validated options; restart after changing JWT settings.

appsettings.json supplies non-sensitive defaults: issuer Kwestie, audience Kwestie.Api, and a 15-minute lifetime. It deliberately contains no signing key. For local development, provide an independently generated, high-entropy signing key through the API's User Secrets. From the repository root, replace the placeholder locally (do not use the placeholder as a key):

```powershell
dotnet user-secrets set "Jwt:Key" "<YOUR_PRIVATE_RANDOM_KEY_AT_LEAST_32_UTF8_BYTES>" --project src/backend/Kwestie.Api
```

Do not put the actual key in source, documentation, logs, or committed configuration. Existing User Secrets are not changed by the implementation or tests. Builds and JWT tests need no real signing key; starting the API requires one.

### Refresh-token configuration

AddRefreshTokens(configuration) is separate from AddInfrastructure and AddJwtAuthentication. It binds RefreshTokenOptions, validates RefreshTokens:LifetimeDays > 0 at startup, registers IRefreshTokenService as scoped, and uses TryAddSingleton for TimeProvider.System to preserve a custom test clock. API invokes all three registrations. appsettings.json supplies a non-sensitive lifetime of 30 days. No refresh-token signing key exists, and JwtOptions is unchanged.

## Migrations

Migrations belong to Infrastructure. `20260924234735_InitialCreate` exists and was applied locally before the real integration test was added. The local `Kwestie` database exists. Neither the test nor startup creates databases or applies migrations automatically; the test presupposes this local setup.

`20260925192607_AddIdentity` exists and was applied manually to the local `Kwestie` database. It is recorded in `__EFMigrationsHistory` and created AspNetUsers, AspNetUserClaims, AspNetUserLogins, and AspNetUserTokens in the same database used by `KwestieDbContext`.

`20260929220522_AddRefreshTokens` is applied to the local Kwestie database. It adds only RefreshTokens, its columns, primary key, cascading user FK, unique TokenHash index, UserId index, and rowversion. The snapshot includes that model; existing Kwesties and Identity schema and older migrations are unchanged. The local database update check confirmed that the database was already up to date, with no pending migrations. Login with refresh issuance and Refresh have passed their real SQL integration tests.

Database-free checks verify that EF's model matches the snapshot. HasPendingModelChanges = false describes model/snapshot agreement, not migration application to SQL Server. SQL tests requiring RefreshTokens have passed. The full suite passed on 2026-09-30: 93 tests, 93 passed, 0 failed, 0 skipped. No test creates a database or applies migrations automatically.

EF Core Design is a private tooling dependency in Infrastructure and the API startup project, supporting the Infrastructure target/API startup workflow. Future migration generation and application remain manual steps after model review and local User Secrets configuration.

## Current Implementation Scope

Implemented: SQL Server context and mapping, repository insertion with saving, dependency injection registration, shared API/test User Secrets configuration, InitialCreate, AddIdentity, and AddRefreshTokens applied locally, a verified real SQL Server repository round-trip test, and the Identity infrastructure base with Guid users and EF stores. Model, materialization, and Identity registration tests are available without a database.

Register, credential validation, JWT, refresh issuance/rotation, Login's two-token result, and the Refresh use case have real SQL Server coverage. With AddRefreshTokens applied locally, the complete Login + Refresh + JWT flow has passed. API is configured for Bearer validation; workspace/membership checks, logout, authentication HTTP endpoints, and Angular authentication integration remain pending. Authentication is not complete.
