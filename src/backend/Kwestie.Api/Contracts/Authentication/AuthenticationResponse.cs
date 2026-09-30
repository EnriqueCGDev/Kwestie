namespace Kwestie.Api.Contracts.Authentication;

public sealed record AuthenticationResponse(
    Guid UserId,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAtUtc,
    DateTimeOffset RefreshTokenExpiresAtUtc);
