using Kwestie.Application.Authentication.Tokens;

namespace Kwestie.Application.Authentication.Refresh;

public sealed class RefreshSessionHandler(IRefreshTokenService refreshTokens, IAccessTokenGenerator accessTokenGenerator)
{
    private readonly IRefreshTokenService _refreshTokens = refreshTokens;
    private readonly IAccessTokenGenerator _accessTokenGenerator = accessTokenGenerator;

    public async Task<RefreshSessionResult> HandleAsync(
        RefreshSessionCommand command, CancellationToken cancellationToken = default)
    {
        var rotation = await _refreshTokens.RotateAsync(command.RefreshToken, cancellationToken);
        if (!rotation.Succeeded)
            return RefreshSessionResult.InvalidToken();

        var userId = rotation.UserId!.Value;
        var access = _accessTokenGenerator.Generate(userId);
        return RefreshSessionResult.Success(userId, access.AccessToken, access.ExpiresAtUtc,
            rotation.RefreshToken!, rotation.ExpiresAtUtc!.Value);
    }
}
