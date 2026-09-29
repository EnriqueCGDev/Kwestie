using Kwestie.Application.Authentication.Login;

namespace Kwestie.Application.Tests.Authentication.Login;

public class LoginUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_ForwardsInputAndCancellationAndReturnsUserId()
    {
        var id = Guid.NewGuid();
        var fake = new AuthenticationFake(LoginUserResult.Success(id));
        var handler = new LoginUserHandler(fake);
        var command = new LoginUserCommand("test@example.com", "Test-only-Password1!");
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(command, cancellation.Token);

        Assert.Equal(1, fake.Calls);
        Assert.Equal(command.Email, fake.Email);
        Assert.True(command.Password == fake.Password);
        Assert.Equal(cancellation.Token, fake.Token);
        Assert.True(result.Succeeded);
        Assert.Equal(id, result.UserId);
    }

    [Fact]
    public async Task HandleAsync_InvalidCredentials_ReturnsFailureWithoutUserId()
    {
        var fake = new AuthenticationFake(LoginUserResult.InvalidCredentials());
        var handler = new LoginUserHandler(fake);

        var result = await handler.HandleAsync(new LoginUserCommand("test@example.com", "invalid"));

        Assert.False(result.Succeeded);
        Assert.Null(result.UserId);
        Assert.Equal(1, fake.Calls);
    }

    [Fact]
    public void Success_EmptyUserId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => LoginUserResult.Success(Guid.Empty));
    }

    private sealed class AuthenticationFake(LoginUserResult result) : IUserAuthentication
    {
        private readonly LoginUserResult _result = result;

        public int Calls { get; private set; }
        public string? Email { get; private set; }
        public string? Password { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<LoginUserResult> AuthenticateAsync(
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
