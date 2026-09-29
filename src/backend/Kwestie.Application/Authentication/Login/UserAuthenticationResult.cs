namespace Kwestie.Application.Authentication.Login;

public sealed class UserAuthenticationResult
{
    public bool Succeeded => UserId.HasValue;
    public Guid? UserId { get; }

    private UserAuthenticationResult(Guid? userId)
    {
        UserId = userId;
    }

    public static UserAuthenticationResult Success(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("Successful authentication requires a non-empty user ID.", nameof(userId));

        return new(userId);
    }

    public static UserAuthenticationResult InvalidCredentials() => new(null);
}
