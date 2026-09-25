# Infrastructure Layer

## Responsibility

Infrastructure implements technical details required by Application and depends on Application and Domain. Domain contains no EF Core references or persistence attributes.

## Persistence

EF Core 10 and its SQL Server provider are configured through `KwestieDbContext`. The context receives typed options and exposes `Kwesties`. `KwestieConfiguration` applies Fluent API mapping. The API registers the context and scoped repository through `AddInfrastructure(connectionString)`; registration does not create or connect to the database.

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

EF Core Design is a private tooling dependency in Infrastructure and the API startup project, supporting the Infrastructure target/API startup workflow. Future migration generation and application remain manual steps after model review and local User Secrets configuration.

## Current Implementation Scope

Implemented: SQL Server context and mapping, repository insertion with saving, dependency injection registration, shared API/test User Secrets configuration, InitialCreate, and a verified real SQL Server repository round-trip test. Existing model and materialization tests remain available without a database.

Pending: workspace/membership checks and an HTTP endpoint for Create Kwestie. The use case remains unexposed.
