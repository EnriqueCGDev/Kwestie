using Kwestie.Application.Kwesties;
using Kwestie.Application.Kwesties.Get;
using Kwestie.Application.Kwesties.List;
using Kwestie.Application.Workspaces;
using Kwestie.Domain.Kwesties;
using Kwestie.Domain.Workspaces;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Tests.Kwesties.Get;

public class GetKwestieHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    public async Task HandleAsync_ActiveMember_ForwardsAllIdentifiersAndCancellationAndReturnsDetails(WorkspaceRole role)
    {
        var query = new GetKwestieQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var workspaces = AccessibleWorkspace(query, role);
        var details = new KwestieDetails(query.KwestieId, "Printer", "Cannot print",
            KwestieStatus.Open, KwestiePriority.Normal, Now, Now, null, null);
        var kwesties = new RecordingRepository { Result = Task.FromResult<KwestieDetails?>(details) };
        var handler = new GetKwestieHandler(kwesties, workspaces);
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(query, cancellation.Token);

        Assert.Same(details, result);
        Assert.Equal(1, workspaces.CheckCalls);
        Assert.Equal(query.WorkspaceId, workspaces.ReceivedWorkspaceId);
        Assert.Equal(query.UserId, workspaces.ReceivedUserId);
        Assert.Equal(cancellation.Token, workspaces.ReceivedCancellationToken);
        Assert.Equal(1, kwesties.GetCalls);
        Assert.Equal(query.WorkspaceId, kwesties.ReceivedWorkspaceId);
        Assert.Equal(query.KwestieId, kwesties.ReceivedKwestieId);
        Assert.Equal(cancellation.Token, kwesties.ReceivedCancellationToken);
    }

    [Theory]
    [InlineData("MissingWorkspace")]
    [InlineData("OtherUser")]
    [InlineData("InactiveMembership")]
    public async Task HandleAsync_WithoutActiveMembership_ReturnsNullWithoutReadingKwesties(string scenario)
    {
        var query = new GetKwestieQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var workspaces = new RecordingWorkspaceRepository
        {
            WorkspaceId = scenario == "MissingWorkspace" ? null : query.WorkspaceId,
            Member = new WorkspaceMember(query.WorkspaceId,
                scenario == "OtherUser" ? Guid.NewGuid() : query.UserId, WorkspaceRole.Admin, Now),
            IsActive = scenario != "InactiveMembership"
        };
        var kwesties = new RecordingRepository();
        var handler = new GetKwestieHandler(kwesties, workspaces);

        var result = await handler.HandleAsync(query);

        Assert.Null(result);
        Assert.Equal(1, workspaces.CheckCalls);
        Assert.Equal(0, kwesties.GetCalls);
    }

    [Fact]
    public async Task HandleAsync_RepositoryReturnsNoMatchingKwestie_ReturnsNull()
    {
        var query = new GetKwestieQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var kwesties = new RecordingRepository();
        var handler = new GetKwestieHandler(kwesties, AccessibleWorkspace(query));

        Assert.Null(await handler.HandleAsync(query));
        Assert.Equal(1, kwesties.GetCalls);
        Assert.Equal(query.WorkspaceId, kwesties.ReceivedWorkspaceId);
        Assert.Equal(query.KwestieId, kwesties.ReceivedKwestieId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WaitsForMembershipBeforeReading(bool allowed)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var workspaces = new RecordingWorkspaceRepository { Completion = completion.Task };
        var kwesties = new RecordingRepository();
        var handler = new GetKwestieHandler(kwesties, workspaces);

        var pending = handler.HandleAsync(new GetKwestieQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()));

        Assert.False(pending.IsCompleted);
        Assert.Equal(0, kwesties.GetCalls);
        completion.SetResult(allowed);
        Assert.Null(await pending);
        Assert.Equal(allowed ? 1 : 0, kwesties.GetCalls);
    }

    [Fact]
    public async Task HandleAsync_WaitsForRepositoryBeforeReturning()
    {
        var query = new GetKwestieQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var completion = new TaskCompletionSource<KwestieDetails?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var kwesties = new RecordingRepository { Result = completion.Task };
        var handler = new GetKwestieHandler(kwesties, AccessibleWorkspace(query));

        var pending = handler.HandleAsync(query);

        Assert.Equal(1, kwesties.GetCalls);
        Assert.False(pending.IsCompleted);
        completion.SetResult(null);
        Assert.Null(await pending);
    }

    [Fact]
    public async Task HandleAsync_MembershipFailure_PropagatesWithoutReadingKwesties()
    {
        var failure = new InvalidOperationException("Access query failed.");
        var workspaces = new RecordingWorkspaceRepository { Completion = Task.FromException<bool>(failure) };
        var kwesties = new RecordingRepository();
        var handler = new GetKwestieHandler(kwesties, workspaces);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.HandleAsync(new GetKwestieQuery(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid())));

        Assert.Same(failure, exception);
        Assert.Equal(0, kwesties.GetCalls);
    }

    private static RecordingWorkspaceRepository AccessibleWorkspace(
        GetKwestieQuery query, WorkspaceRole role = WorkspaceRole.Admin) => new()
    {
        WorkspaceId = query.WorkspaceId,
        Member = new WorkspaceMember(query.WorkspaceId, query.UserId, role, Now)
    };

    private sealed class RecordingRepository : IKwestieRepository
    {
        public Task<KwestieEntity?> GetForUpdateAsync(Guid workspaceId, Guid kwestieId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> UpdateAsync(KwestieEntity kwestie,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public int GetCalls { get; private set; }
        public Guid ReceivedWorkspaceId { get; private set; }
        public Guid ReceivedKwestieId { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }
        public Task<KwestieDetails?> Result { get; init; } = Task.FromResult<KwestieDetails?>(null);

        public Task<KwestieDetails?> GetForWorkspaceAsync(
            Guid workspaceId, Guid kwestieId, CancellationToken cancellationToken = default)
        {
            GetCalls++;
            ReceivedWorkspaceId = workspaceId;
            ReceivedKwestieId = kwestieId;
            ReceivedCancellationToken = cancellationToken;
            return Result;
        }

        public Task AddAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<KwestieSummary>> ListForWorkspaceAsync(
            Guid workspaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingWorkspaceRepository : IWorkspaceRepository
    {
        public Task<WorkspaceRole?> GetActiveRoleAsync(Guid workspaceId, Guid userId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

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
