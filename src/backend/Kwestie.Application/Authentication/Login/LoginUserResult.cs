namespace Kwestie.Application.Authentication.Login;

public sealed class LoginUserResult
{
    public bool Succeeded => UserId.HasValue;
    public Guid? UserId { get; }
    public string? AccessToken { get; }
    public DateTimeOffset? ExpiresAtUtc { get; }

    private LoginUserResult(Guid? userId, string? accessToken, DateTimeOffset? expiresAtUtc)
    {
        UserId = userId;
        AccessToken = accessToken;
        ExpiresAtUtc = expiresAtUtc;
    }

    public static LoginUserResult Success(Guid userId, string accessToken, DateTimeOffset expiresAtUtc)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A successful login requires a non-empty user ID.", nameof(userId));

        ArgumentException.ThrowIfNullOrWhiteSpace(accessToken);
        if (expiresAtUtc == default || expiresAtUtc.Offset != TimeSpan.Zero)
            throw new ArgumentException("Token expiration must be a non-default UTC timestamp.", nameof(expiresAtUtc));

        return new(userId, accessToken, expiresAtUtc);
    }

    public static LoginUserResult InvalidCredentials() => new(null, null, null);
}
