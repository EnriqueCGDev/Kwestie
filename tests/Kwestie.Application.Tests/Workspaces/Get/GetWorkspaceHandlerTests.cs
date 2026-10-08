using Kwestie.Application.Workspaces;
using Kwestie.Application.Workspaces.Get;
using Kwestie.Application.Workspaces.List;
using Kwestie.Domain.Workspaces;

namespace Kwestie.Application.Tests.Workspaces.Get;

public class GetWorkspaceHandlerTests
{
    [Fact]
    public async Task HandleAsync_ForwardsIdsAndCancellationAndMapsFoundWorkspace()
    {
        var createdAt = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var workspace = new Workspace(Guid.NewGuid(), "Development", createdAt);
        var repository = new RecordingRepository { Completion = Task.FromResult<Workspace?>(workspace) };
        var handler = new GetWorkspaceHandler(repository);
        var query = new GetWorkspaceQuery(workspace.Id, Guid.NewGuid());
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(query, cancellation.Token);

        Assert.Equal(1, repository.GetCalls);
        Assert.Equal(query.WorkspaceId, repository.ReceivedWorkspaceId);
        Assert.Equal(query.UserId, repository.ReceivedUserId);
        Assert.Equal(cancellation.Token, repository.ReceivedCancellationToken);
        Assert.Equal(new WorkspaceSummary(workspace.Id, workspace.Name, createdAt), result);
    }

    [Fact]
    public async Task HandleAsync_UnavailableWorkspace_ReturnsNull()
    {
        var repository = new RecordingRepository();
        var handler = new GetWorkspaceHandler(repository);

        Assert.Null(await handler.HandleAsync(new GetWorkspaceQuery(Guid.NewGuid(), Guid.NewGuid())));
        Assert.Equal(1, repository.GetCalls);
    }

    [Fact]
    public async Task HandleAsync_WaitsForRepositoryBeforeReturningResult()
    {
        var completion = new TaskCompletionSource<Workspace?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingRepository { Completion = completion.Task };
        var handler = new GetWorkspaceHandler(repository);

        var pending = handler.HandleAsync(new GetWorkspaceQuery(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal(1, repository.GetCalls);
        Assert.False(pending.IsCompleted);
        completion.SetResult(null);
        Assert.Null(await pending);
    }

    private sealed class RecordingRepository : IWorkspaceRepository
    {
        public Task<Workspace?> Completion { get; init; } = Task.FromResult<Workspace?>(null);
        public int GetCalls { get; private set; }
        public Guid ReceivedWorkspaceId { get; private set; }
        public Guid ReceivedUserId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<Workspace?> GetForUserAsync(
            Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            ReceivedWorkspaceId = workspaceId;
            ReceivedUserId = userId;
            ReceivedCancellationToken = cancellationToken;
            return Completion;
        }

        public Task AddAsync(Workspace workspace, WorkspaceMember initialMember,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Workspace>> ListForUserAsync(
            Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> HasActiveMembershipAsync(
            Guid workspaceId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
