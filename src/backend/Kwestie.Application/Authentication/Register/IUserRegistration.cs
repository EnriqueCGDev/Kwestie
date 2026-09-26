namespace Kwestie.Application.Authentication.Register;

public interface IUserRegistration
{
    Task<RegisterUserResult> RegisterAsync(
        string email, string password, CancellationToken cancellationToken = default);
}
