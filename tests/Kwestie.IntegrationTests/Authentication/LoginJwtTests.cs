using Kwestie.Application.Authentication.Login;
using Kwestie.Application.Authentication.Register;
using Kwestie.Application.Authentication.Tokens;
using Kwestie.Infrastructure;
using Kwestie.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kwestie.IntegrationTests.Authentication;

public class LoginJwtTests
{
    [Fact]
    public async Task Login_RealUser_ReturnsValidJwt_AndInvalidCredentialsReturnNoToken()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddUserSecrets<LoginJwtTests>(optional: true);
        var connectionString = configuration.GetConnectionString("Kwestie");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Kwestie is required in shared API User Secrets; AddIdentity must already be applied.");

        var services = new ServiceCollection();
        services.AddInfrastructure(connectionString);
        services.AddJwtAuthentication(JwtTestConfiguration.Create());
        await using var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(connectionString).Options;
        var email = $"LoginJwt-{Guid.NewGuid():N}@example.com";
        var password = $"Test-{Guid.NewGuid():N}-Aa1!";

        await using (var modelContext = new KwestieDbContext(options))
            Assert.False(modelContext.Database.HasPendingModelChanges());

        try
        {
            Guid userId;
            await using (var registerScope = provider.CreateAsyncScope())
            {
                var registered = await registerScope.ServiceProvider.GetRequiredService<IUserRegistration>()
                    .RegisterAsync(email, password);
                Assert.True(registered.Succeeded);
                userId = registered.UserId!.Value;
            }

            await using var loginScope = provider.CreateAsyncScope();
            var handler = new LoginUserHandler(
                loginScope.ServiceProvider.GetRequiredService<IUserAuthentication>(),
                loginScope.ServiceProvider.GetRequiredService<IAccessTokenGenerator>());
            var result = await handler.HandleAsync(new LoginUserCommand(email, password));
            Assert.True(result.Succeeded);
            Assert.Equal(userId, result.UserId);
            Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
            Assert.NotNull(result.ExpiresAtUtc);

            var bearer = provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
                .Get(JwtBearerDefaults.AuthenticationScheme);
            var validated = await bearer.TokenHandlers.Single().ValidateTokenAsync(
                result.AccessToken, bearer.TokenValidationParameters);
            Assert.True(validated.IsValid);
            Assert.Equal(userId.ToString(), validated.ClaimsIdentity.FindFirst("sub")?.Value);
            Assert.Equal(result.ExpiresAtUtc.Value.UtcDateTime, validated.SecurityToken.ValidTo);
            Assert.Equal(TimeSpan.FromMinutes(15), validated.SecurityToken.ValidTo - validated.SecurityToken.ValidFrom);

            var wrongPassword = await handler.HandleAsync(new LoginUserCommand(email, "Wrong-Test-Password1!"));
            var missingUser = await handler.HandleAsync(new LoginUserCommand($"Missing-{Guid.NewGuid():N}@example.com", password));
            foreach (var rejected in new[] { wrongPassword, missingUser })
            {
                Assert.False(rejected.Succeeded);
                Assert.Null(rejected.UserId);
                Assert.Null(rejected.AccessToken);
                Assert.Null(rejected.ExpiresAtUtc);
            }
        }
        finally
        {
            await using var cleanupContext = new KwestieDbContext(options);
            await cleanupContext.Users.Where(user => user.Email == email).ExecuteDeleteAsync();
            Assert.False(await cleanupContext.Users.AnyAsync(user => user.Email == email));
        }
    }
}
