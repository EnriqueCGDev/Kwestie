using Kwestie.Application.Kwesties;
using Kwestie.Application.Kwesties.List;
using Kwestie.Application.Workspaces;
using Kwestie.Domain.Kwesties;
using Kwestie.Domain.Workspaces;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Tests.Kwesties.List;

public class ListKwestiesHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 9, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    public async Task HandleAsync_ActiveMember_ForwardsIdentifiersAndCancellationAndReturnsSummaries(WorkspaceRole role)
    {
        var query = new ListKwestiesQuery(Guid.NewGuid(), Guid.NewGuid());
        var workspaces = AccessibleWorkspace(query, role);
        IReadOnlyList<KwestieSummary> summaries = [new(
            Guid.NewGuid(), "Printer", "Cannot print", KwestieStatus.Open, KwestiePriority.Normal, Now)];
        var kwesties = new RecordingRepository { Result = Task.FromResult(summaries) };
        var handler = new ListKwestiesHandler(kwesties, workspaces);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(query, cancellation.Token);

        Assert.Same(summaries, result);
        Assert.Equal(1, workspaces.CheckCalls);
        Assert.Equal(query.WorkspaceId, workspaces.ReceivedWorkspaceId);
        Assert.Equal(query.UserId, workspaces.ReceivedUserId);
        Assert.Equal(cancellation.Token, workspaces.ReceivedCancellationToken);
        Assert.Equal(1, kwesties.ListCalls);
        Assert.Equal(query.WorkspaceId, kwesties.ReceivedWorkspaceId);
        Assert.Equal(cancellation.Token, kwesties.ReceivedCancellationToken);
    }

    [Theory]
    [InlineData("MissingWorkspace")]
    [InlineData("OtherUser")]
    [InlineData("InactiveMembership")]
    public async Task HandleAsync_WithoutActiveMembership_RejectsWithoutReadingKwesties(string scenario)
    {
        var query = new ListKwestiesQuery(Guid.NewGuid(), Guid.NewGuid());
        var workspaces = new RecordingWorkspaceRepository
        {
            WorkspaceId = scenario == "MissingWorkspace" ? null : query.WorkspaceId,
            Member = new WorkspaceMember(query.WorkspaceId,
                scenario == "OtherUser" ? Guid.NewGuid() : query.UserId, WorkspaceRole.Admin, Now),
            IsActive = scenario != "InactiveMembership"
        };
        var kwesties = new RecordingRepository();
        var handler = new ListKwestiesHandler(kwesties, workspaces);

        var result = await handler.HandleAsync(query);

        Assert.Null(result);
        Assert.Equal(1, workspaces.CheckCalls);
        Assert.Equal(0, kwesties.ListCalls);
    }

    [Fact]
    public async Task HandleAsync_AccessibleEmptyWorkspace_ReturnsEmptyListInsteadOfRejection()
    {
        var query = new ListKwestiesQuery(Guid.NewGuid(), Guid.NewGuid());
        var kwesties = new RecordingRepository();
        var handler = new ListKwestiesHandler(kwesties, AccessibleWorkspace(query));

        var result = await handler.HandleAsync(query);

        Assert.NotNull(result);
        Assert.Empty(result);
        Assert.Equal(1, kwesties.ListCalls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WaitsForMembershipCheckBeforeReading(bool allowed)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workspaces = new RecordingWorkspaceRepository { Completion = completion.Task };
        var kwesties = new RecordingRepository();
        var handler = new ListKwestiesHandler(kwesties, workspaces);

        var pending = handler.HandleAsync(new ListKwestiesQuery(Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(pending.IsCompleted);
        Assert.Equal(0, kwesties.ListCalls);
        completion.SetResult(allowed);
        var result = await pending;
        Assert.Equal(allowed ? 1 : 0, kwesties.ListCalls);
        Assert.Equal(allowed, result is not null);
    }

    [Fact]
    public async Task HandleAsync_WaitsForKwestiesBeforeReturning()
    {
        var query = new ListKwestiesQuery(Guid.NewGuid(), Guid.NewGuid());
        var completion = new TaskCompletionSource<IReadOnlyList<KwestieSummary>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var kwesties = new RecordingRepository { Result = completion.Task };
        var handler = new ListKwestiesHandler(kwesties, AccessibleWorkspace(query));

        var pending = handler.HandleAsync(query);

        Assert.Equal(1, kwesties.ListCalls);
        Assert.False(pending.IsCompleted);
        completion.SetResult([]);
        Assert.Empty((await pending)!);
    }

    [Fact]
    public async Task HandleAsync_MembershipQueryFailure_PropagatesWithoutReadingKwesties()
    {
        var failure = new InvalidOperationException("Access query failed.");
        var workspaces = new RecordingWorkspaceRepository { Completion = Task.FromException<bool>(failure) };
        var kwesties = new RecordingRepository();
        var handler = new ListKwestiesHandler(kwesties, workspaces);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new ListKwestiesQuery(Guid.NewGuid(), Guid.NewGuid())));

        Assert.Same(failure, exception);
        Assert.Equal(0, kwesties.ListCalls);
    }

    private static RecordingWorkspaceRepository AccessibleWorkspace(
        ListKwestiesQuery query, WorkspaceRole role = WorkspaceRole.Admin) => new()
    {
        WorkspaceId = query.WorkspaceId,
        Member = new WorkspaceMember(query.WorkspaceId, query.UserId, role, Now)
    };

    private sealed class RecordingRepository : IKwestieRepository
    {
        public int ListCalls { get; private set; }
        public Guid ReceivedWorkspaceId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }
        public Task<IReadOnlyList<KwestieSummary>> Result { get; init; } =
            Task.FromResult<IReadOnlyList<KwestieSummary>>([]);

        public Task<IReadOnlyList<KwestieSummary>> ListForWorkspaceAsync(
            Guid workspaceId, CancellationToken cancellationToken = default)
        {
            ListCalls++;
            ReceivedWorkspaceId = workspaceId;
            ReceivedCancellationToken = cancellationToken;
            return Result;
        }

        public Task AddAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private sealed class RecordingWorkspaceRepository : IWorkspaceRepository
    {
        public Guid? WorkspaceId { get; init; }
        public WorkspaceMember? Member { get; init; }
        public bool IsActive { get; init; } = true;
        public Task<bool>? Completion { get; init; }
        public int CheckCalls { get; private set; }
        public Guid ReceivedWorkspaceId { get; private set; }
        public Guid ReceivedUserId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public Task<bool> HasActiveMembershipAsync(
            Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
        {
            CheckCalls++;
            ReceivedWorkspaceId = workspaceId;
            ReceivedUserId = userId;
            ReceivedCancellationToken = cancellationToken;
            return Completion ?? Task.FromResult(WorkspaceId == workspaceId &&
                Member?.WorkspaceId == workspaceId && Member.UserId == userId && IsActive);
        }

        public Task AddAsync(Workspace workspace, WorkspaceMember initialMember,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<Workspace>> ListForUserAsync(
            Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Workspace?> GetForUserAsync(Guid workspaceId, Guid userId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
