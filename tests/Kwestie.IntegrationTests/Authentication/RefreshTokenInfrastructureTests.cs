using Kwestie.Application.Authentication.Tokens;
using Kwestie.Infrastructure;
using Kwestie.Infrastructure.Authentication;
using Kwestie.Infrastructure.Identity;
using Kwestie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kwestie.IntegrationTests.Authentication;

public class RefreshTokenInfrastructureTests
{
    private const string Connection = "Server=unused;Database=RefreshModelTests;Integrated Security=true";

    [Fact]
    public void Model_StoresOnlyHash_WithUserCascadeAndConcurrency()
    {
        using var context = new KwestieDbContext(new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(Connection).Options);
        var entity = context.Model.FindEntityType(typeof(RefreshToken))!;
        Assert.Equal("RefreshTokens", entity.GetTableName());
        var key = Assert.Single(entity.FindPrimaryKey()!.Properties);
        Assert.Equal(nameof(RefreshToken.Id), key.Name);
        Assert.Equal(typeof(Guid), key.ClrType);
        Assert.Equal(ValueGenerated.Never, key.ValueGenerated);
        var hash = entity.FindProperty(nameof(RefreshToken.TokenHash))!;
        Assert.Equal("char(64)", hash.GetColumnType());
        Assert.Equal(64, hash.GetMaxLength());
        Assert.False(hash.IsUnicode());
        Assert.True(hash.IsFixedLength());
        Assert.False(hash.IsNullable);
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.SequenceEqual(new[] { hash }));
        var fk = Assert.Single(entity.GetForeignKeys());
        Assert.Equal(typeof(ApplicationUser), fk.PrincipalEntityType.ClrType);
        Assert.Equal(nameof(RefreshToken.UserId), Assert.Single(fk.Properties).Name);
        Assert.True(fk.IsRequired);
        Assert.Equal(DeleteBehavior.Cascade, fk.DeleteBehavior);
        var version = entity.FindProperty(nameof(RefreshToken.RowVersion))!;
        Assert.True(version.IsConcurrencyToken);
        Assert.Equal("rowversion", version.GetColumnType());
        Assert.Equal(ValueGenerated.OnAddOrUpdate, version.ValueGenerated);
        Assert.False(version.IsNullable);
        foreach (var name in new[] { nameof(RefreshToken.CreatedAtUtc), nameof(RefreshToken.ExpiresAtUtc), nameof(RefreshToken.RevokedAtUtc) })
        {
            var timestamp = entity.FindProperty(name)!;
            Assert.Equal("datetimeoffset", timestamp.GetColumnType());
            Assert.Equal(name == nameof(RefreshToken.RevokedAtUtc), timestamp.IsNullable);
        }
        Assert.Equal(new[] { "CreatedAtUtc", "ExpiresAtUtc", "Id", "RevokedAtUtc", "RowVersion", "TokenHash", "UserId" },
            entity.GetProperties().Select(property => property.Name).OrderBy(name => name).ToArray());
        Assert.Equal(7, typeof(RefreshToken).GetProperties().Length);
        Assert.False(context.Database.HasPendingModelChanges());
    }

    [Fact]
    public void Registration_IsScoped_AndPreservesCustomClock_WithoutJwt()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(Connection);
        var clock = new TestClock();
        services.AddSingleton<TimeProvider>(clock);
        services.AddRefreshTokens(Configuration("30"));
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var service = first.ServiceProvider.GetRequiredService<IRefreshTokenService>();
        Assert.IsType<RefreshTokenService>(service);
        Assert.Same(service, first.ServiceProvider.GetRequiredService<IRefreshTokenService>());
        Assert.NotSame(service, second.ServiceProvider.GetRequiredService<IRefreshTokenService>());
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
        Assert.Equal(30, provider.GetRequiredService<IOptions<RefreshTokenOptions>>().Value.LifetimeDays);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-1")]
    public async Task Startup_RequiresPositiveLifetime(string? days)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Services.AddInfrastructure(Connection);
        builder.Services.AddRefreshTokens(Configuration(days));
        using var host = builder.Build();
        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains("RefreshTokens:LifetimeDays", exception.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("not-a-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAB")]
    public async Task Rotate_MalformedToken_ReturnsInvalidWithoutDatabase(string? token)
    {
        await using var context = new KwestieDbContext(new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(Connection).Options);
        var service = new RefreshTokenService(context, Options.Create(new RefreshTokenOptions { LifetimeDays = 30 }), TimeProvider.System);
        var result = await service.RotateAsync(token);
        Assert.False(result.Succeeded);
        Assert.Null(result.UserId);
        Assert.Null(result.RefreshToken);
        Assert.Null(result.ExpiresAtUtc);
        Assert.Empty(context.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Issue_EmptyUserId_IsRejectedBeforeDatabaseAccess()
    {
        await using var context = new KwestieDbContext(new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(Connection).Options);
        var service = new RefreshTokenService(context, Options.Create(new RefreshTokenOptions { LifetimeDays = 30 }), TimeProvider.System);
        await Assert.ThrowsAsync<ArgumentException>(() => service.IssueAsync(Guid.Empty));
    }

    private static IConfiguration Configuration(string? days) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?> { ["RefreshTokens:LifetimeDays"] = days }).Build();

    private sealed class TestClock : TimeProvider;
}
