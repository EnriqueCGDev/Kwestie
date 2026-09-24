# Application Layer

## Responsibility

Application orchestrates use cases, depends on Domain, and defines the abstractions it needs from external systems. It does not implement infrastructure or duplicate intrinsic rules already enforced by Domain.

## Feature Organization

The implemented feature is organized as follows:

```text
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

`CreateKwestieResult` is an immutable record containing only `Id`, matching the entity sent to the repository. It does not return `Number` or `Key` and does not produce a visible reference. Real persistence, which will assign `Number`, is not implemented; the entity's number remains `0` in the current tests.

## Application Abstractions

`IKwestieRepository` lives in Application and represents the current use case's external dependency. Its only operation is `Task AddAsync(Kwestie kwestie, CancellationToken cancellationToken = default)`. Infrastructure will implement this contract later. There is no repository implementation, `SaveChanges`, or unit of work in this feature.

## Time

The handler uses .NET `TimeProvider` rather than reading the real clock directly. Tests supply a small subclass returning a fixed timestamp; no custom clock interface or additional package is needed.

## Validation Boundaries

Domain enforces intrinsic rules using the entity's state and input data. Application leaves those checks to the constructor without duplicating, catching, or translating them.

Checks requiring other data or coordination belong to Application orchestration. Workspace existence, creator membership, category/workspace compatibility, active membership, and authorization are not implemented. Create Kwestie must not be exposed through an API endpoint until the required workspace and membership checks exist.

## Current Implementation Scope

Implemented: `CreateKwestieCommand`, `CreateKwestieHandler`, `CreateKwestieResult`, `IKwestieRepository`, and feature tests in `Kwestie.Application.Tests/Kwesties/Create`.

Tests use a local recording repository fake and a fixed time provider. They cover the created entity and result, generated ID, timestamps, unassigned number, cancellation-token forwarding, waiting for the repository, and domain rejection without a repository call.

Real persistence, number generation, visible references, cross-entity checks, authorization, DI registration, and an API endpoint remain outside this implementation.
