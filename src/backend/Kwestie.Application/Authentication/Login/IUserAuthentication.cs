namespace Kwestie.Application.Authentication.Login;

public interface IUserAuthentication
{
    Task<LoginUserResult> AuthenticateAsync(
        string email, string password, CancellationToken cancellationToken = default);
}
