# API

Authentication endpoints are anonymous and use Application handlers. API owns HTTP DTOs and refresh-cookie handling; it does not implement authentication or persistence logic.

Workspace endpoints require a valid Bearer access token. They obtain the user exclusively from the validated JWT `sub`; missing, invalid, or empty Guid subjects return **401 Unauthorized**. No client-supplied user identifier is bound from the body, query, route, or headers.

## POST /api/auth/register

Request:

```json
{ "email": "...", "password": "..." }
```

Success: **201 Created**, without an invented Location header:

```json
{ "userId": "..." }
```

Identity rejection: **400 Bad Request**:

```json
{ "errors": ["..."] }
```

Register does not log in the user or issue tokens. Invalid request binding uses the standard ApiController validation response; password policies remain in Identity.

## POST /api/auth/login

Request:

```json
{ "email": "...", "password": "..." }
```

Success: **200 OK**:

```json
{
  "userId": "...",
  "accessToken": "...",
  "accessTokenExpiresAtUtc": "...",
  "refreshTokenExpiresAtUtc": "..."
}
```

The raw refresh token is delivered only through the `kwestie_refresh_token` cookie, never JSON. Its options are HttpOnly, Secure, SameSite=Strict, Path=/api/auth, Expires=RefreshTokenExpiresAtUtc, with no Domain. HTTPS is required for browser delivery of the secure cookie.

Unknown email and incorrect password return the same **401 Unauthorized** behavior without issuing a cookie.

## POST /api/auth/refresh

No body. Reads the refresh cookie and rotates it through the existing use case. Success returns **200 OK** with the same JSON shape as Login and replaces the cookie using the new refresh token and expiration.

Missing, invalid, expired, revoked, reused, or concurrently consumed tokens return a generic **401 Unauthorized**, without an access token. The cookie is deleted using the same name and Path; the failure does not disclose its cause.

## POST /api/auth/logout

No body. Uses the refresh cookie, revokes only the supplied active refresh token, and deletes the cookie with Path=/api/auth. Always returns **204 No Content** with no tokens or body for valid, missing, malformed, unknown, expired, or already revoked tokens. Repeated calls are idempotent. An access token is not required, and an expired Bearer token does not prevent logout. Already issued access JWTs remain valid until expiration; other sessions are unaffected. Infrastructure failures still propagate rather than claiming successful revocation.

The cookie Path is now /api/auth for issuance, rotation, and deletion, allowing browser delivery to both Refresh and Logout. Cookies from the previous /api/auth/refresh scope must be cleared when updating an existing local browser session.

Angular uses these contracts through an in-memory AuthService and relative URLs via a local development proxy. Login and Register screens are available at `/login` and `/register`. On startup Angular makes one `/api/auth/refresh` attempt using the HttpOnly cookie; failure leaves no session and does not block startup. The access token remains in memory and a Bearer interceptor attaches it only to `/api/...` requests outside `/api/auth/...`. Login navigates to `/app`, a guarded Workspace listing/creation screen with Logout; this is not a functional dashboard. The guard reads the restored session state without calling Refresh. The screen loads Workspaces once on entry and reloads them after successful creation, using the existing Bearer interceptor without sending UserId. Workspace links open /app/workspaces/:workspaceId for an individual GET and Create Kwestie form. Automatic refresh after a 401 and deployment-specific CORS remain pending. Cross-site deployment would require a separate CORS/CSRF decision rather than changing SameSite preemptively.

## POST /api/workspaces

Requires Bearer authentication. Request contains only Name:

```json
{ "name": "Mi Workspace" }
```

Unknown JSON fields, including userId, return **400 Bad Request**. Name validation and trimming remain in Domain; Domain rejections are mapped to **400 Bad Request** with ProblemDetails. The user ID comes only from `sub`. The use case persists the workspace and that user's active Admin membership atomically, sharing CreatedAt/JoinedAt.

Success: **201 Created**, retaining the existing response contract without a Location header:

```json
{ "workspaceId": "..." }
```

## GET /api/workspaces

Requires Bearer authentication. Returns **200 OK** with only the workspaces where the JWT user has an active membership, or an empty array when none exist:

```json
[
  { "workspaceId": "...", "name": "Mi Workspace", "createdAt": "2026-10-07T12:00:00+00:00" }
]
```

Results are ordered by CreatedAt ascending, then WorkspaceId ascending. This is query behavior, not a Domain rule. No user filter is accepted from the client. Workspace listing/creation UI and the Create Kwestie endpoint below are implemented. Individual Workspace navigation and Create Kwestie UI are implemented; membership management remains pending.

## GET /api/workspaces/{workspaceId}

Requires a valid Bearer JWT and a valid, non-empty Guid sub, as for the other Workspace endpoints. WorkspaceId is bound only from the route; UserId comes only from the validated JWT. No identifier is accepted from body, query, or headers.

An existing workspace with an active Admin or Member membership returns **200 OK**, reusing WorkspaceResponse:

```json
{ "workspaceId": "550e8400-e29b-41d4-a716-446655440000", "name": "Desarrollo", "createdAt": "2026-10-08T12:00:00Z" }
```

Only WorkspaceId, Name, and CreatedAt are returned, without memberships, roles, or user information. Missing Workspace, foreign user, and inactive membership all return the same **404 Not Found** with an empty body. Missing/invalid authentication or an invalid sub returns **401 Unauthorized**. For authenticated requests, a malformed/non-Guid route identifier returns **400 Bad Request** through standard Guid model binding; a well-formed but unavailable identifier returns 404.

Application uses one membership-filtered repository read, without a preceding HasActiveMembershipAsync call. The existing POST and list contracts are unchanged. Angular loads this endpoint directly from the guarded Workspace route, including direct entry/reload; it does not resolve the selected Workspace through the list.

## POST /api/workspaces/{workspaceId}/kwesties

Requires a valid Bearer access token and a non-empty Guid `sub`, using the same identity rules as Workspace endpoints. WorkspaceId comes only from the route; CreatedById comes only from the validated JWT. Query/header values cannot supply either identifier.

Request accepts only required Title, optional Description, and required numeric Priority:

```json
{ "title": "Impresora descompuesta", "description": "No imprime desde esta mañana", "priority": 2 }
```

Priority values are 1 = Low, 2 = Normal, 3 = High, and 4 = Critical. No global enum serialization change is made. Domain validates and trims Title, trims Description (omitted/null becomes an empty string), and validates the defined priority; no length limits are imposed. Unknown JSON properties, including createdById, workspaceId, categoryId, id, number, and status, are rejected. CategoryId is always null in the Application command.

KwestiesController delegates to CreateKwestieHandler; it does not repeat the workspace/membership check. Active Admin and Member memberships are permitted.

After persistence, **201 Created** returns only:

```json
{ "kwestieId": "..." }
```

The ID comes from CreateKwestieResult. There is no Location header, Number, Key, or visible reference. SQL Server still generates Number using the existing mapping.

Errors:

- **400 Bad Request** with ProblemDetails for Domain invariant failures, missing Priority, invalid JSON/types, or unknown fields.
- **401 Unauthorized** for missing/invalid JWT or missing, malformed, or empty Guid sub.
- **403 Forbidden**, with an empty body, for CreateKwestieAccessDeniedException. Missing Workspace, foreign user, and inactive membership yield the same response without disclosing the reason.

HTTP/SQL tests use real JWT validation and isolated users/workspaces, verify committed data from a separate context, and clean up their own rows in finally. Angular supports this POST from the individual Workspace screen, with numeric priority and confirmation only; authentication headers come from the existing interceptor. The backend list is implemented below; the Angular Kwestie list, individual Kwestie GET/detail, Categories, assignment, and state-changing endpoints remain unimplemented.

## GET /api/workspaces/{workspaceId}/kwesties

Requires a valid Bearer JWT and a non-empty Guid sub. WorkspaceId comes exclusively from the route and UserId from the validated subject, following the existing Workspace identity rules. Active Admin and Member memberships are permitted.

**200 OK** returns persisted Kwesties ordered by CreatedAt ascending, then Id ascending. An accessible Workspace without Kwesties returns `[]`.

```json
[
  {
    "kwestieId": "550e8400-e29b-41d4-a716-446655440000",
    "title": "Impresora descompuesta",
    "description": "No imprime",
    "status": 1,
    "priority": 2,
    "createdAt": "2026-10-09T12:00:00Z"
  }
]
```

These are the only response fields. Status is numeric: 1 = Open, 2 = InProgress, 3 = Resolved, 4 = Closed. Priority retains 1 = Low, 2 = Normal, 3 = High, 4 = Critical. No global enum serialization is changed. Number, Key, creator/assignee/category IDs, and other technical fields are excluded; visible Number/Key references remain undefined.

- **401 Unauthorized** for missing/invalid JWT or missing, malformed, or empty Guid sub.
- **404 Not Found**, uniformly empty, for nonexistent Workspace, foreign user, or inactive membership.
- **400 Bad Request** for an authenticated request with a malformed/non-Guid WorkspaceId, through existing model binding.

Application checks HasActiveMembershipAsync before reading Kwesties; rejected access never invokes the list repository. Infrastructure performs a workspace-filtered, ordered, asynchronous projection without tracking or writes. HTTP/SQL tests cover the real persisted results, empty responses, isolation, order, exact contract, and indistinguishable rejections. Angular listing, individual Kwestie GET, filters/search/pagination, and visible references remain pending. POST creation is unchanged.
