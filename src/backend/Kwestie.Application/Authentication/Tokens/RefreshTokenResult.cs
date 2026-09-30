namespace Kwestie.Application.Authentication.Tokens;

public sealed class RefreshTokenResult
{
    public string RefreshToken { get; }
    public DateTimeOffset ExpiresAtUtc { get; }

    public RefreshTokenResult(string refreshToken, DateTimeOffset expiresAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(refreshToken);
        if (expiresAtUtc == default || expiresAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Expiration must be a non-default UTC timestamp.", nameof(expiresAtUtc));

        RefreshToken = refreshToken;
        ExpiresAtUtc = expiresAtUtc;
    }
}
