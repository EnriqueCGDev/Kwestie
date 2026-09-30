using Kwestie.Application.Authentication.Logout;
using Kwestie.Application.Authentication.Tokens;

namespace Kwestie.Application.Tests.Authentication.Logout;

public class LogoutSessionHandlerTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("test-refresh")]
    public async Task HandleAsync_ForwardsTokenAndCancellation_AndWaitsForRevocation(string? token)
    {
        var service = new RefreshFake();
        using var cancellation = new CancellationTokenSource();
        var pending = new LogoutSessionHandler(service).HandleAsync(new LogoutSessionCommand(token), cancellation.Token);
        Assert.Equal(token, service.Token);
        Assert.Equal(cancellation.Token, service.Cancellation);
        Assert.False(pending.IsCompleted);
        service.Completion.SetResult();
        await pending;
    }

    private sealed class RefreshFake : IRefreshTokenService
    {
        public string? Token { get; private set; }
        public CancellationToken Cancellation { get; private set; }
        public TaskCompletionSource Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task RevokeAsync(string? refreshToken, CancellationToken cancellationToken = default)
        {
            Token = refreshToken;
            Cancellation = cancellationToken;
            return Completion.Task;
        }

        public Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
        public Task<RefreshTokenRotationResult> RotateAsync(string? refreshToken, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }
}
