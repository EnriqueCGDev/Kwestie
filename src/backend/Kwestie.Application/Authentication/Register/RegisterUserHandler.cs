namespace Kwestie.Application.Authentication.Register;

public sealed class RegisterUserHandler(IUserRegistration registration)
{
    public Task<RegisterUserResult> HandleAsync(
        RegisterUserCommand command, CancellationToken cancellationToken = default) =>
        registration.RegisterAsync(command.Email, command.Password, cancellationToken);
}
