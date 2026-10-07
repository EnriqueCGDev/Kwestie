using Kwestie.Application.Workspaces;
using Kwestie.Application.Workspaces.List;
using Kwestie.Domain.Workspaces;

namespace Kwestie.Application.Tests.Workspaces.List;

public class ListWorkspacesHandlerTests
{
    [Fact]
    public async Task HandleAsync_ForwardsUserAndCancellationAndMapsWorkspaceSummaries()
    {
        var createdAt = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
        var workspace = new Workspace(Guid.NewGuid(), "Support", createdAt);
        var repository = new RecordingRepository
        {
            Completion = Task.FromResult<IReadOnlyList<Workspace>>([workspace])
        };
        var handler = new ListWorkspacesHandler(repository);
        var query = new ListWorkspacesQuery(Guid.NewGuid());
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(query, cancellation.Token);

        Assert.Equal(1, repository.ListCalls);
        Assert.Equal(query.UserId, repository.ReceivedUserId);
        Assert.Equal(cancellation.Token, repository.ReceivedCancellationToken);
        Assert.Equal(new WorkspaceSummary(workspace.Id, workspace.Name, createdAt), Assert.Single(result));
    }

    [Fact]
    public async Task HandleAsync_WaitsForRepositoryBeforeReturningResult()
    {
        var completion = new TaskCompletionSource<IReadOnlyList<Workspace>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingRepository { Completion = completion.Task };
        var handler = new ListWorkspacesHandler(repository);

        var pending = handler.HandleAsync(new ListWorkspacesQuery(Guid.NewGuid()));

        Assert.Equal(1, repository.ListCalls);
        Assert.False(pending.IsCompleted);
        completion.SetResult([]);
        Assert.Empty(await pending);
    }

    private sealed class RecordingRepository : IWorkspaceRepository
    {
        public Task<IReadOnlyList<Workspace>> Completion { get; init; } =
            Task.FromResult<IReadOnlyList<Workspace>>([]);
        public int ListCalls { get; private set; }
        public Guid ReceivedUserId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<IReadOnlyList<Workspace>> ListForUserAsync(
            Guid userId, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            ReceivedUserId = userId;
            ReceivedCancellationToken = cancellationToken;
            return Completion;
        }

        public Task AddAsync(Workspace workspace, WorkspaceMember initialMember,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
