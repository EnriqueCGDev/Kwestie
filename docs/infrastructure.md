# Infrastructure Layer

## Responsibility

Infrastructure implements technical details required by Application and depends on Application and Domain. Domain contains no EF Core references or persistence attributes.

## Persistence

EF Core 10 and its SQL Server provider are configured through `KwestieDbContext`. The context receives typed options and exposes `Kwesties`. `KwestieConfiguration` applies Fluent API mapping. The API registers the context and scoped repository through `AddInfrastructure(connectionString)`; registration does not create or connect to the database.

## Identity

ASP.NET Core Identity is integrated exclusively in Infrastructure. `Identity/ApplicationUser` derives from `IdentityUser<Guid>` without additional properties. Domain and Application do not depend on Identity types.

`KwestieDbContext` derives from `IdentityUserContext<ApplicationUser, Guid>`, sharing the existing SQL Server `Kwestie` database. It calls the base model configuration before applying `KwestieConfiguration` and retains its `Kwesties` DbSet. `CreatedById` and `AssignedToId` remain Guid references without navigations or foreign keys to ApplicationUser.

`AddInfrastructure` registers Identity Core with EF stores and sets `options.User.RequireUniqueEmail = true`. Default password policies remain unchanged. There are no global role services or role tables in this model; future Workspace Admin/Member roles are unrelated to global Identity roles.

AspNetUsers, AspNetUserClaims, AspNetUserLogins, and AspNetUserTokens are mapped in the model and now exist physically in the local SQL Server `Kwestie` database after the manual application of `AddIdentity`. These tables do not imply implemented external-login or application refresh-token features. No authentication cookies, external providers, default token providers, or endpoints are registered. JWT, refresh tokens, email confirmation, and password recovery remain unimplemented.

### User registration

`Identity/UserRegistration` implements Application's `IUserRegistration` using `UserManager<ApplicationUser>`. It generates a Guid, sets Email and UserName to the submitted email, and calls `CreateAsync(user, password)`. Identity validates the input and password, hashes the password, normalizes email/username, and persists the user in AspNetUsers. The adapter neither assigns PasswordHash nor writes directly through the DbContext. Identity error descriptions become Application-owned strings in RegisterUserResult.

The adapter is scoped in AddInfrastructure; Application handlers are not registered there. Cancellation is checked before CreateAsync, which does not accept a CancellationToken. Register returns only a user ID or errors, without tokens or an HTTP endpoint.

A real SQL Server test registers a unique email, reads the user through a separate context, checks normalized values and the stored hash, verifies the password through Identity's PasswordHasher, and confirms rejection of the same email. Its finally block removes only users with that test's unique email and verifies cleanup. No credentials or hashes are logged.

Database-free tests verify Guid user keys, Identity user tables, the absence of roles and Kwestie-to-user foreign keys, and resolution of UserManager with an EF user store sharing the registered context.

### User authentication

`Identity/UserAuthentication` implements Application's `IUserAuthentication` and is registered as scoped in `AddInfrastructure`. It uses `UserManager<ApplicationUser>.FindByEmailAsync` and `CheckPasswordAsync`; it does not query users through DbContext, read or compare PasswordHash manually, or invoke PasswordHasher directly. Its primary constructor initializes a private readonly UserManager field.

A valid password returns the user's Guid. An unknown email and an incorrect password return the same public result: `Succeeded = false`, `UserId = null`. The adapter exposes no reason-specific errors and logs no credentials. This does not guarantee identical execution timing. No JWT, refresh token, cookie, or session is created.

Existing Identity policies remain unchanged. CheckPasswordAsync does not increment failed-access counts or enforce lockout; no additional lockout is implemented. Identity may upgrade an outdated password hash on successful verification. Cancellation is checked before the lookup and before password validation, since these UserManager methods have no CancellationToken parameter.

A real SQL Server test creates a unique user through the existing Register adapter, validates correct credentials from a fresh scope, and verifies equivalent rejection results for a wrong password and a missing email. It deletes only its test user in finally and verifies removal. The test checks for pending EF model changes before any write. Login changes no database model and requires no new migration.

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

## Migrations

Migrations belong to Infrastructure. `20260924234735_InitialCreate` exists and was applied locally before the real integration test was added. The local `Kwestie` database exists. Neither the test nor startup creates databases or applies migrations automatically; the test presupposes this local setup.

`20260925192607_AddIdentity` exists and was applied manually to the local `Kwestie` database. It is recorded in `__EFMigrationsHistory` and created AspNetUsers, AspNetUserClaims, AspNetUserLogins, and AspNetUserTokens in the same database used by `KwestieDbContext`.

The KwestieRepository round-trip test accesses Kwesties. Identity model and service-registration tests remain database-free; the new user-registration integration test exercises AspNetUsers on SQL Server. Register and RequireUniqueEmail do not change the schema, and the test verifies that EF reports no pending model changes before writing. No new migration was required.

EF Core Design is a private tooling dependency in Infrastructure and the API startup project, supporting the Infrastructure target/API startup workflow. Future migration generation and application remain manual steps after model review and local User Secrets configuration.

## Current Implementation Scope

Implemented: SQL Server context and mapping, repository insertion with saving, dependency injection registration, shared API/test User Secrets configuration, InitialCreate and AddIdentity applied locally, a verified real SQL Server repository round-trip test, and the Identity infrastructure base with Guid users and EF stores. Model, materialization, and Identity registration tests are available without a database.

Register and Login are implemented through UserManager with real SQL Server coverage. Pending: JWT and refresh tokens, workspace/membership checks, and HTTP endpoints for Register, Login, and Create Kwestie. All three use cases remain unexposed; authentication is not complete.
