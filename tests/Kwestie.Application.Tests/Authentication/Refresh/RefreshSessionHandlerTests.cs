using Kwestie.Application.Authentication.Refresh;
using Kwestie.Application.Authentication.Tokens;

namespace Kwestie.Application.Tests.Authentication.Refresh;

public class RefreshSessionHandlerTests
{
    private static readonly DateTimeOffset Expiration = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_UsesPersistedUserAndRotatedToken_AndForwardsCancellation()
    {
        var userId = Guid.NewGuid();
        var refresh = new RefreshFake(RefreshTokenRotationResult.Success(userId, "new-test-refresh", Expiration.AddDays(30)));
        var access = new AccessFake();
        var handler = new RefreshSessionHandler(refresh, access);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(new RefreshSessionCommand("old-test-refresh"), cancellation.Token);

        Assert.True(refresh.ReceivedToken == "old-test-refresh");
        Assert.Equal(cancellation.Token, refresh.Cancellation);
        Assert.Equal(1, refresh.Calls);
        Assert.Equal(1, access.Calls);
        Assert.Equal(userId, access.UserId);
        Assert.True(result.Succeeded);
        Assert.Equal(userId, result.UserId);
        Assert.True(result.AccessToken == "new-test-access");
        Assert.True(result.RefreshToken == "new-test-refresh");
        Assert.Equal(Expiration, result.AccessTokenExpiresAtUtc);
        Assert.Equal(Expiration.AddDays(30), result.RefreshTokenExpiresAtUtc);
    }

    [Fact]
    public async Task HandleAsync_InvalidRefresh_DoesNotGenerateAccessToken()
    {
        var refresh = new RefreshFake(RefreshTokenRotationResult.InvalidToken());
        var access = new AccessFake();
        var result = await new RefreshSessionHandler(refresh, access).HandleAsync(new RefreshSessionCommand(null));

        Assert.False(result.Succeeded);
        Assert.Null(result.UserId);
        Assert.Null(result.AccessToken);
        Assert.Null(result.AccessTokenExpiresAtUtc);
        Assert.Null(result.RefreshToken);
        Assert.Null(result.RefreshTokenExpiresAtUtc);
        Assert.Equal(0, access.Calls);
        Assert.Equal(1, refresh.Calls);
    }

    [Fact]
    public void Success_EmptyUserId_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => RefreshSessionResult.Success(Guid.Empty, "access", Expiration, "refresh", Expiration));
        Assert.Throws<ArgumentException>(() => RefreshTokenRotationResult.Success(Guid.Empty, "refresh", Expiration));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public void Success_EmptyTokens_AreRejected(string? token)
    {
        Assert.ThrowsAny<ArgumentException>(() => RefreshSessionResult.Success(Guid.NewGuid(), token!, Expiration, "refresh", Expiration));
        Assert.ThrowsAny<ArgumentException>(() => RefreshSessionResult.Success(Guid.NewGuid(), "access", Expiration, token!, Expiration));
        Assert.ThrowsAny<ArgumentException>(() => RefreshTokenRotationResult.Success(Guid.NewGuid(), token!, Expiration));
        Assert.ThrowsAny<ArgumentException>(() => new RefreshTokenResult(token!, Expiration));
    }

    [Fact]
    public void Success_InvalidExpirations_AreRejected()
    {
        foreach (var invalid in new[] { default(DateTimeOffset), Expiration.ToOffset(TimeSpan.FromHours(1)) })
        {
            Assert.Throws<ArgumentException>(() => RefreshSessionResult.Success(Guid.NewGuid(), "access", invalid, "refresh", Expiration));
            Assert.Throws<ArgumentException>(() => RefreshSessionResult.Success(Guid.NewGuid(), "access", Expiration, "refresh", invalid));
            Assert.Throws<ArgumentException>(() => RefreshTokenRotationResult.Success(Guid.NewGuid(), "refresh", invalid));
            Assert.Throws<ArgumentException>(() => new RefreshTokenResult("refresh", invalid));
        }
    }

    private sealed class RefreshFake(RefreshTokenRotationResult result) : IRefreshTokenService
    {
        private readonly RefreshTokenRotationResult _result = result;
        public string? ReceivedToken { get; private set; }
        public CancellationToken Cancellation { get; private set; }
        public int Calls { get; private set; }

        public Task<RefreshTokenRotationResult> RotateAsync(string? token, CancellationToken cancellationToken = default)
        {
            ReceivedToken = token;
            Cancellation = cancellationToken;
            Calls++;
            return Task.FromResult(_result);
        }

        public Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class AccessFake : IAccessTokenGenerator
    {
        public Guid? UserId { get; private set; }
        public int Calls { get; private set; }
        public AccessTokenResult Generate(Guid userId)
        {
            Calls++;
            UserId = userId;
            return new("new-test-access", Expiration);
        }
    }
}
