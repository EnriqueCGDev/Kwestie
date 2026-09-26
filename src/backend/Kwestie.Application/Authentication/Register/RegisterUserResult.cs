namespace Kwestie.Application.Authentication.Register;

public sealed class RegisterUserResult
{
    public bool Succeeded => UserId.HasValue;
    public Guid? UserId { get; }
    public IReadOnlyList<string> Errors { get; }

    private RegisterUserResult(Guid? userId, IReadOnlyList<string> errors)
    {
        UserId = userId;
        Errors = errors;
    }

    public static RegisterUserResult Success(Guid userId) => new(userId, Array.Empty<string>());

    public static RegisterUserResult Rejected(IEnumerable<string> errors) =>
        new(null, Array.AsReadOnly(errors.ToArray()));
}
