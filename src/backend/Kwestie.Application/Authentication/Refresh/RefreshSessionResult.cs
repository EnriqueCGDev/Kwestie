namespace Kwestie.Application.Authentication.Refresh;

public sealed class RefreshSessionResult
{
    public bool Succeeded => UserId.HasValue;
    public Guid? UserId { get; }
    public string? AccessToken { get; }
    public DateTimeOffset? AccessTokenExpiresAtUtc { get; }
    public string? RefreshToken { get; }
    public DateTimeOffset? RefreshTokenExpiresAtUtc { get; }

    private RefreshSessionResult(Guid? userId, string? accessToken, DateTimeOffset? accessTokenExpiresAtUtc,
        string? refreshToken, DateTimeOffset? refreshTokenExpiresAtUtc)
    {
        UserId = userId;
        AccessToken = accessToken;
        AccessTokenExpiresAtUtc = accessTokenExpiresAtUtc;
        RefreshToken = refreshToken;
        RefreshTokenExpiresAtUtc = refreshTokenExpiresAtUtc;
    }

    public static RefreshSessionResult Success(Guid userId, string accessToken, DateTimeOffset accessTokenExpiresAtUtc,
        string refreshToken, DateTimeOffset refreshTokenExpiresAtUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A successful result requires a non-empty user ID.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        if (accessTokenExpiresAtUtc == default || accessTokenExpiresAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Access expiration must be a non-default UTC timestamp.", nameof(accessTokenExpiresAtUtc));
        if (refreshTokenExpiresAtUtc == default || refreshTokenExpiresAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Refresh expiration must be a non-default UTC timestamp.", nameof(refreshTokenExpiresAtUtc));

        return new(userId, accessToken, accessTokenExpiresAtUtc, refreshToken, refreshTokenExpiresAtUtc);
    }

    public static RefreshSessionResult InvalidToken() => new(null, null, null, null, null);
}
