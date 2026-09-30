using Kwestie.Application.Authentication.Tokens;

namespace Kwestie.Application.Authentication.Logout;

public sealed class LogoutSessionHandler(IRefreshTokenService refreshTokens)
{
    private readonly IRefreshTokenService _refreshTokens = refreshTokens;

    public Task HandleAsync(LogoutSessionCommand command, CancellationToken cancellationToken = default) =>
        _refreshTokens.RevokeAsync(command.RefreshToken, cancellationToken);
}
