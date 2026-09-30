using Kwestie.Application.Authentication.Tokens;

namespace Kwestie.Application.Authentication.Login;

public sealed class LoginUserHandler(
    IUserAuthentication authentication,
    IAccessTokenGenerator accessTokenGenerator,
    IRefreshTokenService refreshTokens)
{
    private readonly IUserAuthentication _authentication = authentication;
    private readonly IAccessTokenGenerator _accessTokenGenerator = accessTokenGenerator;
    private readonly IRefreshTokenService _refreshTokens = refreshTokens;

    public async Task<LoginUserResult> HandleAsync(
        LoginUserCommand command, CancellationToken cancellationToken = default)
    {
        var authentication = await _authentication.AuthenticateAsync(
            command.Email, command.Password, cancellationToken);

        if (!authentication.Succeeded)
            return LoginUserResult.InvalidCredentials();

        var userId = authentication.UserId!.Value;
        var token = _accessTokenGenerator.Generate(userId);
        var refresh = await _refreshTokens.IssueAsync(userId, cancellationToken);
        return LoginUserResult.Success(userId, token.AccessToken, token.ExpiresAtUtc,
            refresh.RefreshToken, refresh.ExpiresAtUtc);
    }
}
