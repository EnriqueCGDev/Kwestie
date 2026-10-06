# API

Authentication endpoints are anonymous and use Application handlers. API owns HTTP DTOs and refresh-cookie handling; it does not implement authentication or persistence logic.

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

Angular uses these contracts through an in-memory AuthService and relative URLs via a local development proxy. Login and Register screens are available at `/login` and `/register`. On startup Angular makes one `/api/auth/refresh` attempt using the HttpOnly cookie; failure leaves no session and does not block startup. The access token remains in memory and a Bearer interceptor attaches it only to `/api/...` requests outside `/api/auth/...`. Login navigates to `/app`, a minimal guarded session screen with a Logout button; this is not a functional dashboard. The guard reads the restored session state without calling Refresh. Workspace features, automatic refresh after a 401, and deployment-specific CORS remain pending. Cross-site deployment would require a separate CORS/CSRF decision rather than changing SameSite preemptively.
