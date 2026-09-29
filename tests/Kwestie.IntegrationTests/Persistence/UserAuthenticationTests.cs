using Kwestie.Application.Authentication.Login;
using Kwestie.Application.Authentication.Register;
using Kwestie.Infrastructure;
using Kwestie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kwestie.IntegrationTests.Persistence;

public class UserAuthenticationTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task AuthenticateAsync_ValidatesRealCredentialsAndReturnsEquivalentRejections()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddUserSecrets<UserAuthenticationTests>(optional: true);
        var connectionString = configuration.GetConnectionString("Kwestie");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Kwestie is required in shared API User Secrets; AddIdentity must already be applied.");

        var services = new ServiceCollection();
        services.AddInfrastructure(connectionString);
        await using var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(connectionString).Options;
        var email = $"Login-{Guid.NewGuid():N}@example.com";
        var missingEmail = $"Missing-{Guid.NewGuid():N}@example.com";
        var password = $"Test-{Guid.NewGuid():N}-Aa1!";

        // Stop before any write if the model differs; never apply migrations here.
        await using (var modelContext = new KwestieDbContext(options))
            Assert.False(modelContext.Database.HasPendingModelChanges());

        try
        {
            Guid userId;
            await using (var registrationScope = provider.CreateAsyncScope())
            {
                var registration = registrationScope.ServiceProvider.GetRequiredService<IUserRegistration>();
                var registered = await registration.RegisterAsync(email, password);
                Assert.True(registered.Succeeded);
                Assert.NotNull(registered.UserId);
                userId = registered.UserId.Value;
                Assert.NotEqual(Guid.Empty, userId);
            }

            // A fresh scope forces Identity to load the persisted user.
            await using var loginScope = provider.CreateAsyncScope();
            var authentication = loginScope.ServiceProvider.GetRequiredService<IUserAuthentication>();
            var success = await authentication.AuthenticateAsync(email, password);
            Assert.True(success.Succeeded);
            Assert.Equal(userId, success.UserId);
            _output.WriteLine("Real login succeeded with the registered user ID.");

            var wrongPassword = await authentication.AuthenticateAsync(email, $"Wrong-{Guid.NewGuid():N}-Aa1!");
            Assert.False(wrongPassword.Succeeded);
            Assert.Null(wrongPassword.UserId);

            var missingUser = await authentication.AuthenticateAsync(missingEmail, password);
            Assert.False(missingUser.Succeeded);
            Assert.Null(missingUser.UserId);
            Assert.Equal(wrongPassword.Succeeded, missingUser.Succeeded);
            Assert.Equal(wrongPassword.UserId, missingUser.UserId);
            _output.WriteLine("Wrong password and missing user returned equivalent invalid-credentials results.");
        }
        finally
        {
            await using var cleanupContext = new KwestieDbContext(options);
            await cleanupContext.Users.Where(user => user.Email == email).ExecuteDeleteAsync();
            Assert.False(await cleanupContext.Users.AnyAsync(user => user.Email == email));
            _output.WriteLine("Test user cleanup completed; absence verified.");
        }
    }
}
