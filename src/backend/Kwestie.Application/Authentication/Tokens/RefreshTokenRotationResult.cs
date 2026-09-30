namespace Kwestie.Application.Authentication.Tokens;

public sealed class RefreshTokenRotationResult
{
    public bool Succeeded => UserId.HasValue;
    public Guid? UserId { get; }
    public string? RefreshToken { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }

    private RefreshTokenRotationResult(Guid? userId, string? refreshToken, DateTimeOffset? expiresAtUtc)
    {
        UserId = userId;
        RefreshToken = refreshToken;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static RefreshTokenRotationResult Success(Guid userId, string refreshToken, DateTimeOffset expiresAtUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A successful rotation requires a non-empty user ID.", nameof(userId));
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        if (expiresAtUtc == default || expiresAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Expiration must be a non-default UTC timestamp.", nameof(expiresAtUtc));

        return new(userId, refreshToken, expiresAtUtc);
    }

    public static RefreshTokenRotationResult InvalidToken() => new(null, null, null);
}
