using Kwestie.Application.Authentication.Register;

namespace Kwestie.Application.Tests.Authentication.Register;

public class RegisterUserHandlerTests
{
    [Fact]
    public async Task HandleAsync_ForwardsInputAndCancellationAndReturnsUserId()
    {
        var id = Guid.NewGuid();
        var fake = new RegistrationFake(RegisterUserResult.Success(id));
        var handler = new RegisterUserHandler(fake);
        var command = new RegisterUserCommand("test@example.com", "Test-only-Password1!");
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(command, cancellation.Token);

        Assert.Equal(1, fake.Calls);
        Assert.Equal(command.Email, fake.Email);
        Assert.True(command.Password == fake.Password);
        Assert.Equal(cancellation.Token, fake.Token);
        Assert.True(result.Succeeded);
        Assert.Equal(id, result.UserId);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task HandleAsync_ReturnsRegistrationErrorsWithoutUserId()
    {
        var errors = new[] { "Email rejected", "Password rejected" };
        var fake = new RegistrationFake(RegisterUserResult.Rejected(errors));
        var handler = new RegisterUserHandler(fake);

        var result = await handler.HandleAsync(new RegisterUserCommand("test@example.com", "invalid"));

        Assert.False(result.Succeeded);
        Assert.Null(result.UserId);
        Assert.Equal(errors, result.Errors);
        Assert.Equal(1, fake.Calls);
    }

    private sealed class RegistrationFake(RegisterUserResult result) : IUserRegistration
    {
        public int Calls { get; private set; }
        public string? Email { get; private set; }
        public string? Password { get; private set; }
        public CancellationToken Token { get; private set; }

        public Task<RegisterUserResult> RegisterAsync(string email, string password, CancellationToken cancellationToken = default)
        {
            Calls++;
            Email = email;
            Password = password;
            Token = cancellationToken;
            return Task.FromResult(result);
        }
    }
}
