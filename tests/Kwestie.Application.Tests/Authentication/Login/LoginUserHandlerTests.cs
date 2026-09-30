using Kwestie.Application.Authentication.Login;
using Kwestie.Application.Authentication.Tokens;

namespace Kwestie.Application.Tests.Authentication.Login;

public class LoginUserHandlerTests
{
    private static readonly DateTimeOffset Expiration = new(2030, 1, 1, 12, 15, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_ForwardsInputAndCancellationAndReturnsUserId()
    {
        var id = Guid.NewGuid();
        var fake = new AuthenticationFake(UserAuthenticationResult.Success(id));
        var tokens = new TokenGeneratorFake();
        var refresh = new RefreshTokensFake();
        var handler = new LoginUserHandler(fake, tokens, refresh);
        var command = new LoginUserCommand("test@example.com", "Test-only-Password1!");
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(command, cancellation.Token);

        Assert.Equal(1, fake.Calls);
        Assert.Equal(command.Email, fake.Email);
        Assert.True(command.Password == fake.Password);
        Assert.Equal(cancellation.Token, fake.Token);
        Assert.True(result.Succeeded);
        Assert.Equal(id, result.UserId);
        Assert.Equal(1, tokens.Calls);
        Assert.Equal(id, tokens.UserId);
        Assert.True(result.AccessToken == tokens.Result.AccessToken);
        Assert.Equal(Expiration, result.AccessTokenExpiresAtUtc);
        Assert.Equal(1, refresh.Calls);
        Assert.Equal(id, refresh.UserId);
        Assert.Equal(cancellation.Token, refresh.Token);
        Assert.True(result.RefreshToken == refresh.Result.RefreshToken);
        Assert.Equal(refresh.Result.ExpiresAtUtc, result.RefreshTokenExpiresAtUtc);
    }

    [Fact]
    public async Task HandleAsync_InvalidCredentials_ReturnsFailureWithoutUserId()
    {
        var fake = new AuthenticationFake(UserAuthenticationResult.InvalidCredentials());
        var tokens = new TokenGeneratorFake();
        var refresh = new RefreshTokensFake();
        var handler = new LoginUserHandler(fake, tokens, refresh);

        var result = await handler.HandleAsync(new LoginUserCommand("test@example.com", "invalid"));

        Assert.False(result.Succeeded);
        Assert.Null(result.UserId);
        Assert.Null(result.AccessToken);
        Assert.Null(result.AccessTokenExpiresAtUtc);
        Assert.Null(result.RefreshToken);
        Assert.Null(result.RefreshTokenExpiresAtUtc);
        Assert.Equal(0, refresh.Calls);
        Assert.Equal(0, tokens.Calls);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public void Success_EmptyUserId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => UserAuthenticationResult.Success(Guid.Empty));
        Assert.Throws<ArgumentException>(() => LoginUserResult.Success(Guid.Empty, "test-token", Expiration, "test-refresh", Expiration));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Success_EmptyToken_IsRejected(string? token)
    {
        Assert.ThrowsAny<ArgumentException>(() => LoginUserResult.Success(Guid.NewGuid(), token!, Expiration, "test-refresh", Expiration));
        Assert.ThrowsAny<ArgumentException>(() => LoginUserResult.Success(Guid.NewGuid(), "test-access", Expiration, token!, Expiration));
    }

    [Fact]
    public void Success_InvalidExpiration_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => LoginUserResult.Success(Guid.NewGuid(), "test-token", default, "test-refresh", Expiration));
        Assert.Throws<ArgumentException>(() => LoginUserResult.Success(
            Guid.NewGuid(), "test-token", Expiration.ToOffset(TimeSpan.FromHours(1)), "test-refresh", Expiration));
        Assert.Throws<ArgumentException>(() => LoginUserResult.Success(Guid.NewGuid(), "test-token", Expiration, "test-refresh", default));
        Assert.Throws<ArgumentException>(() => LoginUserResult.Success(
            Guid.NewGuid(), "test-token", Expiration, "test-refresh", Expiration.ToOffset(TimeSpan.FromHours(1))));
    }

    private sealed class RefreshTokensFake : IRefreshTokenService
    {
        public Task RevokeAsync(string? token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public int Calls { get; private set; }
        public Guid? UserId { get; private set; }
        public CancellationToken Token { get; private set; }
        public RefreshTokenResult Result { get; } = new("test-refresh", Expiration.AddDays(30));

        public Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            Calls++;
            UserId = userId;
            Token = cancellationToken;
            return Task.FromResult(Result);
        }

        public Task<RefreshTokenRotationResult> RotateAsync(string? token, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class TokenGeneratorFake : IAccessTokenGenerator
    {
        public int Calls { get; private set; }
        public Guid? UserId { get; private set; }
        public AccessTokenResult Result { get; } = new("test-token", Expiration);

        public AccessTokenResult Generate(Guid userId)
        {
            Calls++;
            UserId = userId;
            return Result;
        }
    }

    private sealed class AuthenticationFake(UserAuthenticationResult result) : IUserAuthentication
    {
        private readonly UserAuthenticationResult _result = result;

        public int Calls { get; private set; }
        public string? Email { get; private set; }
        public string? Password { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<UserAuthenticationResult> AuthenticateAsync(
            string email, string password, CancellationToken cancellationToken = default)
        {
            Calls++;
            Email = email;
            Password = password;
            Token = cancellationToken;
            return Task.FromResult(_result);
        }
    }
}
