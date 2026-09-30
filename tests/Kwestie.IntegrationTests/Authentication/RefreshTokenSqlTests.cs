using System.Security.Cryptography;
using System.Text;
using Kwestie.Application.Authentication.Register;
using Kwestie.Application.Authentication.Tokens;
using Kwestie.Infrastructure;
using Kwestie.Infrastructure.Authentication;
using Kwestie.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kwestie.IntegrationTests.Authentication;

public class RefreshTokenSqlTests
{
    [Fact]
    public Task Issue_PersistsOnlyHash_AndFullLifetime() => WithUser(async (provider, userId, clock) =>
    {
        await using var scope = provider.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<IRefreshTokenService>().IssueAsync(userId);
        Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
        Assert.Equal(32, WebEncoders.Base64UrlDecode(result.RefreshToken).Length);
        Assert.Equal(clock.GetUtcNow().AddDays(30), result.ExpiresAtUtc);
        await using var read = provider.CreateAsyncScope();
        var stored = await read.ServiceProvider.GetRequiredService<KwestieDbContext>().RefreshTokens.AsNoTracking().SingleAsync(t => t.UserId == userId);
        Assert.True(stored.TokenHash == Hash(result.RefreshToken));
        Assert.True(stored.TokenHash != result.RefreshToken);
        Assert.Equal(64, stored.TokenHash.Length);
        Assert.Equal(clock.GetUtcNow(), stored.CreatedAtUtc);
        Assert.Equal(result.ExpiresAtUtc, stored.ExpiresAtUtc);
        Assert.Null(stored.RevokedAtUtc);
        Assert.Equal(8, stored.RowVersion.Length);
    });

    [Fact]
    public Task Rotation_RevokesPreviousToken_AndAllowsReplacementRotation() => WithUser(async (provider, userId, clock) =>
    {
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var first = await service.IssueAsync(userId);
        clock.Advance(TimeSpan.FromDays(1));
        var second = await service.RotateAsync(first.RefreshToken);
        Assert.True(second.Succeeded);
        Assert.Equal(userId, second.UserId);
        Assert.True(second.RefreshToken != first.RefreshToken);
        Assert.Equal(clock.GetUtcNow().AddDays(30), second.ExpiresAtUtc);
        AssertInvalid(await service.RotateAsync(first.RefreshToken));
        await using var read = provider.CreateAsyncScope();
        var context = read.ServiceProvider.GetRequiredService<KwestieDbContext>();
        var rows = await context.RefreshTokens.AsNoTracking().Where(t => t.UserId == userId).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.Equal(clock.GetUtcNow(), rows.Single(t => t.TokenHash == Hash(first.RefreshToken)).RevokedAtUtc);
        Assert.Null(rows.Single(t => t.TokenHash == Hash(second.RefreshToken!)).RevokedAtUtc);
        var third = await service.RotateAsync(second.RefreshToken);
        Assert.True(third.Succeeded);
        Assert.True(third.RefreshToken != second.RefreshToken);
        AssertInvalid(await service.RotateAsync(second.RefreshToken));
    });

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public Task Rotation_RejectsAtAndAfterExpiration(int extraSeconds) => WithUser(async (provider, userId, clock) =>
    {
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        var issued = await service.IssueAsync(userId);
        clock.Advance(TimeSpan.FromDays(30) + TimeSpan.FromSeconds(extraSeconds));
        AssertInvalid(await service.RotateAsync(issued.RefreshToken));
        var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
        Assert.Equal(1, await context.RefreshTokens.CountAsync(t => t.UserId == userId));
    });

    [Fact]
    public Task Rotation_UnknownAndMalformedTokens_ReturnEquivalentFailures() => WithUser(async (provider, userId, clock) =>
    {
        await using var scope = provider.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        AssertInvalid(await service.RotateAsync(WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32))));
        AssertInvalid(await service.RotateAsync("invalid"));
        AssertInvalid(await service.RotateAsync(null));
    });

    [Fact]
    public Task ConcurrentRotation_ExactlyOneWinner_AndNoOrphanReplacement()
    {
        var gate = new RotationSaveBarrier();
        return WithUser(async (provider, userId, clock) =>
        {
            await using var issueScope = provider.CreateAsyncScope();
            var issued = await issueScope.ServiceProvider.GetRequiredService<IRefreshTokenService>().IssueAsync(userId);
            await using var first = provider.CreateAsyncScope();
            await using var second = provider.CreateAsyncScope();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            var attempts = new[]
            {
                first.ServiceProvider.GetRequiredService<IRefreshTokenService>().RotateAsync(issued.RefreshToken, timeout.Token),
                second.ServiceProvider.GetRequiredService<IRefreshTokenService>().RotateAsync(issued.RefreshToken, timeout.Token)
            };
            var results = await Task.WhenAll(attempts);
            Assert.Equal(2, gate.Arrivals);
            Assert.Single(results, result => result.Succeeded);
            AssertInvalid(Assert.Single(results, result => !result.Succeeded));

            // A later save in the losing context must not insert its rolled-back replacement.
            await first.ServiceProvider.GetRequiredService<KwestieDbContext>().SaveChangesAsync();
            await second.ServiceProvider.GetRequiredService<KwestieDbContext>().SaveChangesAsync();
            await using var read = provider.CreateAsyncScope();
            var rows = await read.ServiceProvider.GetRequiredService<KwestieDbContext>().RefreshTokens.AsNoTracking()
                .Where(t => t.UserId == userId).ToListAsync();
            Assert.Equal(2, rows.Count);
            var active = Assert.Single(rows, row => row.RevokedAtUtc is null);
            Assert.True(active.TokenHash == Hash(results.Single(result => result.Succeeded).RefreshToken!));
        }, gate);
    }

    private static void AssertInvalid(RefreshTokenRotationResult result)
    {
        Assert.False(result.Succeeded);
        Assert.Null(result.UserId);
        Assert.Null(result.RefreshToken);
        Assert.Null(result.ExpiresAtUtc);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static async Task WithUser(Func<ServiceProvider, Guid, ControlledClock, Task> test, SaveChangesInterceptor? interceptor = null)
    {
        using var configuration = new ConfigurationManager();
        configuration.AddUserSecrets<RefreshTokenSqlTests>(optional: true);
        var connection = configuration.GetConnectionString("Kwestie");
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("ConnectionStrings:Kwestie is required; manually apply AddRefreshTokens before running these SQL tests.");
        var services = new ServiceCollection();
        services.AddInfrastructure(connection);
        var clock = new ControlledClock();
        services.AddSingleton<TimeProvider>(clock);
        services.AddRefreshTokens(new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["RefreshTokens:LifetimeDays"] = "30" }).Build());
        if (interceptor is not null)
            services.AddDbContext<KwestieDbContext>(options => options.AddInterceptors(interceptor));
        await using var provider = services.BuildServiceProvider();
        var email = $"Refresh-{Guid.NewGuid():N}@example.com";
        await using var setup = provider.CreateAsyncScope();
        var context = setup.ServiceProvider.GetRequiredService<KwestieDbContext>();
        Assert.False(context.Database.HasPendingModelChanges());
        // Fail clearly before creating test data when the manual migration is still pending.
        Assert.Contains(await context.Database.GetAppliedMigrationsAsync(), name => name.EndsWith("_AddRefreshTokens", StringComparison.Ordinal));
        try
        {
            var registration = await setup.ServiceProvider.GetRequiredService<IUserRegistration>()
                .RegisterAsync(email, $"Test-{Guid.NewGuid():N}-Aa1!");
            Assert.True(registration.Succeeded);
            await test(provider, registration.UserId!.Value, clock);
        }
        finally
        {
            await using var cleanup = provider.CreateAsyncScope();
            var db = cleanup.ServiceProvider.GetRequiredService<KwestieDbContext>();
            var ids = await db.Users.Where(user => user.Email == email).Select(user => user.Id).ToArrayAsync();
            await db.Users.Where(user => user.Email == email).ExecuteDeleteAsync();
            Assert.False(await db.Users.AnyAsync(user => user.Email == email));
            Assert.False(await db.RefreshTokens.AnyAsync(token => ids.Contains(token.UserId)));
        }
    }

    private sealed class ControlledClock : TimeProvider
    {
        private DateTimeOffset _now = new(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }

    private sealed class RotationSaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public int Arrivals => _arrivals;

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            eventData.Context!.ChangeTracker.DetectChanges();
            if (eventData.Context.ChangeTracker.Entries<RefreshToken>().Any(entry => entry.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref _arrivals) == 2)
                    _ready.TrySetResult();
                // Both requests have read the same rowversion before either sends its writes.
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
