namespace Kwestie.Application.Authentication.Tokens;

public sealed record AccessTokenResult(string AccessToken, DateTimeOffset ExpiresAtUtc);
