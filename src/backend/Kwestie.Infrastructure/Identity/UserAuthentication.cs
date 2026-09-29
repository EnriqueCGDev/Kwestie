using Kwestie.Application.Authentication.Login;
using Microsoft.AspNetCore.Identity;

namespace Kwestie.Infrastructure.Identity;

public sealed class UserAuthentication(UserManager<ApplicationUser> userManager) : IUserAuthentication
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public async Task<LoginUserResult> AuthenticateAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        // These UserManager operations do not accept a CancellationToken.
        cancellationToken.ThrowIfCancellationRequested();
        var user = await _userManager.FindByEmailAsync(email);
        cancellationToken.ThrowIfCancellationRequested();

        if (user is null || !await _userManager.CheckPasswordAsync(user, password))
            return LoginUserResult.InvalidCredentials();

        return LoginUserResult.Success(user.Id);
    }
}
