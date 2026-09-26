using Kwestie.Application.Authentication.Register;
using Kwestie.Infrastructure;
using Kwestie.Infrastructure.Identity;
using Kwestie.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Kwestie.IntegrationTests.Persistence;

public class UserRegistrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task RegisterAsync_PersistsHashedUserAndRejectsDuplicateEmail()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddUserSecrets<UserRegistrationTests>(optional: true);
        var connectionString = configuration.GetConnectionString("Kwestie");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Kwestie is required in shared API User Secrets; AddIdentity must already be applied.");

        var services = new ServiceCollection();
        services.AddInfrastructure(connectionString);
        await using var provider = services.BuildServiceProvider();
        var options = new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(connectionString).Options;
        var email = $"Register-{Guid.NewGuid():N}@example.com";
        var password = $"Test-{Guid.NewGuid():N}-Aa1!";

        // Check before any write; this test never creates or applies migrations.
        await using (var modelContext = new KwestieDbContext(options))
            Assert.False(modelContext.Database.HasPendingModelChanges());

        try
        {
            await using var scope = provider.CreateAsyncScope();
            var registration = scope.ServiceProvider.GetRequiredService<IUserRegistration>();
            var result = await registration.RegisterAsync(email, password);
            Assert.True(result.Succeeded);
            Assert.Empty(result.Errors);
            Assert.NotNull(result.UserId);
            Assert.NotEqual(Guid.Empty, result.UserId.Value);

            await using var readContext = new KwestieDbContext(options);
            var stored = await readContext.Users.AsNoTracking().SingleAsync(user => user.Id == result.UserId.Value);
            Assert.Equal(result.UserId.Value, stored.Id);
            Assert.Equal(email, stored.Email);
            Assert.Equal(email, stored.UserName);
            Assert.Equal(email.ToUpperInvariant(), stored.NormalizedEmail);
            Assert.Equal(email.ToUpperInvariant(), stored.NormalizedUserName);
            Assert.False(string.IsNullOrWhiteSpace(stored.PasswordHash));
            Assert.False(stored.PasswordHash!.Contains(password, StringComparison.Ordinal));
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<ApplicationUser>>();
            Assert.NotEqual(PasswordVerificationResult.Failed,
                hasher.VerifyHashedPassword(stored, stored.PasswordHash, password));
            output.WriteLine("User recovered from a separate DbContext; stored password hash verified by Identity.");

            await using var duplicateScope = provider.CreateAsyncScope();
            var duplicate = await duplicateScope.ServiceProvider.GetRequiredService<IUserRegistration>()
                .RegisterAsync(email, password);
            Assert.False(duplicate.Succeeded);
            Assert.Null(duplicate.UserId);
            Assert.NotEmpty(duplicate.Errors);
            Assert.Equal(1, await readContext.Users.CountAsync(user => user.Email == email));
            output.WriteLine("Duplicate email rejected.");
        }
        finally
        {
            await using var cleanupContext = new KwestieDbContext(options);
            await cleanupContext.Users.Where(user => user.Email == email).ExecuteDeleteAsync();
            Assert.False(await cleanupContext.Users.AnyAsync(user => user.Email == email));
            output.WriteLine("Only users with this test's unique email were cleaned up; absence verified.");
        }
    }
}
