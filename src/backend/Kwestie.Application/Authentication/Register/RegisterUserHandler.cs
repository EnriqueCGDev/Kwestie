namespace Kwestie.Application.Authentication.Register;

public sealed class RegisterUserHandler(IUserRegistration registration)
{
    private readonly IUserRegistration _registration = registration;

    public Task<RegisterUserResult> HandleAsync(
        RegisterUserCommand command, CancellationToken cancellationToken = default) =>
        _registration.RegisterAsync(command.Email, command.Password, cancellationToken);
}
