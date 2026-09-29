namespace Kwestie.Application.Authentication.Login;

public sealed class LoginUserResult
{
    public bool Succeeded => UserId.HasValue;
    public Guid? UserId { get; }

    private LoginUserResult(Guid? userId)
    {
        UserId = userId;
    }

    public static LoginUserResult Success(Guid userId)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A successful login requires a non-empty user ID.", nameof(userId));

        return new(userId);
    }

    public static LoginUserResult InvalidCredentials() => new(null);
}
