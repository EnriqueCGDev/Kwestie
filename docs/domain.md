# Domain Model

This document describes the current domain decisions for Kwestie.

It evolves together with the implementation and is the source of truth for domain rules unless a decision is explicitly revised.

## Kwestie

A `Kwestie` represents an issue, request, or piece of work that requires attention within a workspace.

It is the central entity of the initial domain.

Current properties are:

```text
Id
Number
WorkspaceId
Title
Description
Status
Priority
CreatedById
AssignedToId
CategoryId
CreatedAt
UpdatedAt
ResolvedAt
ClosedAt
```

`Number` is implemented as a `long` property with a private setter. It is not received by the public constructor, and no manual assignment method is provided.

A newly created, non-persisted Kwestie has `Number = 0`, which represents an unassigned number.

Persisted Kwesties must have `Number > 0`.

The visible reference is only produced after `Number` has been assigned. A value such as `KW-0` must never be exposed as a valid reference.

## Technical Identity and Visible Reference

`Id` is the technical identifier and uses a `Guid`.

The current entity receives `Id` through its constructor instead of generating it internally.

`Number` will be a global numeric sequence in V1 and uses a `long` value.

Example:

```text
Id     = 77a4562b-...
Number = 142
```

The initial visible reference is planned to be derived from `Number` after persistence:

```text
KW-142
```

Current decisions:

- `Number` is global in V1, not per workspace.
- `Number` is expected to be assigned during persistence.
- `Key` does not currently exist as a Domain property.
- A human-readable reference such as `KW-142` is planned to be derived from `Number` after persistence.
- Workspace-specific prefixes are not part of V1.
- Rules about changing future workspace prefixes are intentionally out of scope.

The persistence implementation will be responsible for guaranteeing uniqueness of the generated `Number`.

Sequential values may contain gaps; the visible reference must not depend on gapless numbering.

## Status

The initial lifecycle contains four states:

```text
Open = 1
InProgress = 2
Resolved = 3
Closed = 4
```

Current valid forward transitions are:

```text
Open
  |
  v
InProgress
  |
  v
Resolved
  |
  v
Closed
```

The current implementation intentionally supports only these transitions.

An invalid transition raises `DomainException`.

A rejected transition must not change status or timestamps.

Reopening is a possible future capability, but its rules are not defined yet. No implementation should assume which states may be reopened or which state reopening should target.

## Priority

Initial priorities are:

```text
Low = 1
Normal = 2
High = 3
Critical = 4
```

Priority is part of the domain state.

Changing priority is not implemented yet.

The constructor rejects undefined `KwestiePriority` values with `DomainException`.

## Creation Rules

The current implementation:

- receives `Id`
- receives `WorkspaceId`
- receives `CreatedById`
- receives `CreatedAt`
- initializes `Number` to `0` (unassigned)
- requires `Title`
- rejects null, empty, or whitespace-only titles
- stores `Title` using `Trim()`
- stores `Description` using `Trim()`
- converts a null description to `string.Empty`
- starts in `Open`
- receives its initial priority explicitly
- initializes `UpdatedAt` to the same value as `CreatedAt`
- starts without an assignee
- starts without `ResolvedAt`
- starts without `ClosedAt`
- may optionally receive `CategoryId`

The constructor enforces these invariants:

- `Id` must not be `Guid.Empty`
- `WorkspaceId` must not be `Guid.Empty`
- `CreatedById` must not be `Guid.Empty`
- `CategoryId`, when present, must not be `Guid.Empty`
- `Priority` must be a defined `KwestiePriority` value

Violations of these invariants raise `DomainException` and are covered by unit tests.

Existence or workspace membership of related identifiers cannot be established by the `Kwestie` entity alone.

## User References

`CreatedById` and `AssignedToId` represent user identifiers, not `WorkspaceMember` identifiers.

A user may belong to multiple workspaces.

The relationship between the user and the current workspace is checked outside the entity when the relevant use case is implemented.

Historical references should remain meaningful even if a user later leaves or becomes inactive in a workspace.

The exact behavior for an already-assigned user who later becomes inactive has not yet been defined.

## Encapsulation

Domain state must not be freely mutable from outside the entity.

Application code should not do this:

```csharp
kwestie.Status = KwestieStatus.Closed;
```

Instead, domain behavior should express the operation:

```csharp
kwestie.Close(occurredAt);
```

This allows the entity to enforce its own invariants.

Future behavior may include:

```text
AssignTo
Unassign
ChangePriority
EditDetails
Reopen
```

These operations must not be implemented until their rules are defined.

## Time

Domain operations receive their timestamp instead of directly calling `DateTimeOffset.UtcNow`.

Example:

```csharp
kwestie.Resolve(occurredAt);
```

Current behavior:

- `StartProgress` updates `UpdatedAt`.
- `Resolve` updates `UpdatedAt` and sets `ResolvedAt`.
- `Close` updates `UpdatedAt` and sets `ClosedAt`.
- Closing does not clear `ResolvedAt`.
- Rejected transitions must leave existing timestamps unchanged.

Chronological rules such as rejecting a transition timestamp earlier than the previous change have not yet been defined.

Application obtains creation timestamps through .NET `TimeProvider`; Domain receives them from the caller.

## Resolved vs Closed

`Resolved` means the work has been completed or a resolution has been provided.

`Closed` means the Kwestie has completed its lifecycle and is considered finished.

The Domain currently enforces only the sequence:

```text
InProgress -> Resolved -> Closed
```

Rules describing which user is allowed to close a Kwestie belong to Application/authorization and are not yet defined.

## Workspaces

A `Workspace` represents the boundary in which Kwesties, members, and future categories exist.

`Workspace`, `WorkspaceMember`, and `WorkspaceRole` are implemented in Domain.

`Workspace` has read-only `Id`, `Name`, and `CreatedAt` properties. Its constructor rejects an empty Guid ID and null, empty, or whitespace-only names with `DomainException`. It stores `Name` using `Trim()` and receives `CreatedAt` from outside Domain.

`WorkspaceMember` has read-only `WorkspaceId`, `UserId`, `Role`, `JoinedAt`, and `IsActive` properties. Its constructor rejects empty workspace/user IDs and undefined roles with `DomainException`. `JoinedAt` is supplied by the caller, and a new membership always starts active.

A user may belong to multiple workspaces and may have a different role in each workspace.

Workspace roles are therefore not modeled as global ASP.NET Core Identity roles.

Current roles are:

```text
Admin = 1
Member = 2
```

The Create Workspace use case creates an active Admin membership for the creating user, with the same WorkspaceId and `JoinedAt` equal to the workspace's `CreatedAt`.

Workspace persistence is implemented in Infrastructure, and `20261007152213_AddWorkspaces` was applied manually to the local Kwestie database. Real SQL Server tests verify the Workspace/membership round trip, deletion behavior, and atomic persistence.

Creating a Kwestie requires an existing Workspace and an active membership for the creator. Both Admin and Member may create Kwesties; no additional role restriction applies. Application enforces this rule through its repository contract before persistence, while the Kwestie constructor retains its intrinsic invariants. Access rejection is represented in Application, not by DomainException.

Workspace create/list API and UI are implemented. Protected Create/List Kwesties HTTP endpoints, Angular Workspace navigation, and Kwestie creation/listing UI are implemented. Categories, membership management, and individual Kwestie GET/detail remain pending. Role changes, deactivation/reactivation, member removal, and ownership transfer are not implemented; their rules remain undefined.

## Categories

Categories belong to a specific workspace.

A Kwestie must not use a category from another workspace.

This rule cannot currently be verified by the `Kwestie` entity because it only holds `CategoryId`.

When category-related use cases are implemented, Application will obtain the required information and enforce the workspace relationship before invoking domain behavior.

Category deactivation rather than destructive deletion is the current planned direction once categories are in use.

## Assignment

A Kwestie starts unassigned.

When assignment is implemented:

- the assignee will be referenced by user ID
- the user must belong to the same workspace
- membership/activity checks will be orchestrated by Application

The exact behavior for inactive or removed members is still pending.

## Comments

Comments are planned to belong to a Kwestie and have an author.

The initial direction is a chronological conversation model without nested replies or reactions.

Soft deletion is the current intended approach so conversation history is not silently removed.

Comments are not implemented yet.

## History

Functional history is planned for important Kwestie changes.

Examples may include:

```text
Kwestie created
Assignee changed
Priority changed
Status changed
Kwestie reopened
```

History should be generated automatically by the system rather than manually entered by users.

Domain events are a possible implementation mechanism, but no decision requires them yet.

History is not implemented yet.

## Current Implementation Scope

The current implemented scope is:

```text
Kwestie creation behavior
Number initialized to 0 (unassigned)
Constructor identifier and priority invariants
Open -> InProgress
InProgress -> Resolved
Resolved -> Closed
DomainException on invalid transitions
Unit tests for creation invariants and current lifecycle behavior
Workspace creation invariants and trimmed Name
WorkspaceMember creation invariants and initially active membership
WorkspaceRole (Admin and Member)
```

Assignment of `Number` during persistence and the derived visible reference remain unimplemented.

Persistence, repositories, and authentication remain outside Domain. Workspace membership changes, assignment, comments, history, and reopening remain unimplemented.

## Working Agreement

When implementing domain changes:

- Respect the rules documented here.
- Distinguish implemented behavior from accepted future decisions.
- Do not add behavior whose rules have not been defined.
- Do not silently change existing domain decisions.
- If implementation appears to require changing a documented rule, explain the reason before making the change.
- Keep Domain free from infrastructure and persistence concerns.
- Update this document when a planned rule becomes implemented or when an accepted decision changes.
