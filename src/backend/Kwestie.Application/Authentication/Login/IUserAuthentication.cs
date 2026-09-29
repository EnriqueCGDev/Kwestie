namespace Kwestie.Application.Authentication.Login;

public interface IUserAuthentication
{
    Task<UserAuthenticationResult> AuthenticateAsync(
        string email, string password, CancellationToken cancellationToken = default);
}
