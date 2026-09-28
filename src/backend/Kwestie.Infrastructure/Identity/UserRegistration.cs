using Kwestie.Application.Authentication.Register;
using Microsoft.AspNetCore.Identity;

namespace Kwestie.Infrastructure.Identity;

public sealed class UserRegistration(UserManager<ApplicationUser> userManager) : IUserRegistration
{
    private readonly UserManager<ApplicationUser> _userManager = userManager;

    public async Task<RegisterUserResult> RegisterAsync(
        string email, string password, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var user = new ApplicationUser
        {
            Id = Guid.NewGuid(),
            Email = email,
            UserName = email
        };

        // UserManager.CreateAsync has no CancellationToken overload.
        var result = await _userManager.CreateAsync(user, password);
        return result.Succeeded
            ? RegisterUserResult.Success(user.Id)
            : RegisterUserResult.Rejected(result.Errors.Select(error => error.Description));
    }
}
