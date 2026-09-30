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

The raw refresh token is delivered only through the `kwestie_refresh_token` cookie, never JSON. Its options are HttpOnly, Secure, SameSite=Strict, Path=/api/auth/refresh, Expires=RefreshTokenExpiresAtUtc, with no Domain. HTTPS is required for browser delivery of the secure cookie.

Unknown email and incorrect password return the same **401 Unauthorized** behavior without issuing a cookie.

## POST /api/auth/refresh

No body. Reads the refresh cookie and rotates it through the existing use case. Success returns **200 OK** with the same JSON shape as Login and replaces the cookie using the new refresh token and expiration.

Missing, invalid, expired, revoked, reused, or concurrently consumed tokens return a generic **401 Unauthorized**, without an access token. The cookie is deleted using the same name and Path; the failure does not disclose its cause.

Logout, Angular authentication integration, and CORS are not implemented. The intended browser integration keeps the access token in memory and lets the browser send the HttpOnly refresh cookie; JavaScript does not read or store it. Cross-site deployment would require a separate CORS/CSRF decision rather than changing SameSite preemptively.
