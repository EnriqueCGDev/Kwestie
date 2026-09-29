namespace Kwestie.Application.Authentication.Login;

public sealed class LoginUserHandler(IUserAuthentication authentication)
{
    private readonly IUserAuthentication _authentication = authentication;

    public Task<LoginUserResult> HandleAsync(
        LoginUserCommand command, CancellationToken cancellationToken = default) =>
        _authentication.AuthenticateAsync(command.Email, command.Password, cancellationToken);
}
