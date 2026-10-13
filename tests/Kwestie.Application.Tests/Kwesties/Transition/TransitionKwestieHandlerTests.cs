using Kwestie.Application.Kwesties;
using Kwestie.Application.Kwesties.Get;
using Kwestie.Application.Kwesties.List;
using Kwestie.Application.Kwesties.Transition;
using Kwestie.Application.Workspaces;
using Kwestie.Domain.Kwesties;
using Kwestie.Domain.Workspaces;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Tests.Kwesties.Transition;

public class TransitionKwestieHandlerTests
{
    private static readonly DateTimeOffset Created = new(2026, 10, 12, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Now = Created.AddHours(4);

    [Theory]
    [InlineData(WorkspaceRole.Admin, KwestieTransition.StartProgress, KwestieStatus.Open, KwestieStatus.InProgress)]
    [InlineData(WorkspaceRole.Admin, KwestieTransition.Resolve, KwestieStatus.InProgress, KwestieStatus.Resolved)]
    [InlineData(WorkspaceRole.Admin, KwestieTransition.Close, KwestieStatus.Resolved, KwestieStatus.Closed)]
    [InlineData(WorkspaceRole.Member, KwestieTransition.StartProgress, KwestieStatus.Open, KwestieStatus.InProgress)]
    [InlineData(WorkspaceRole.Member, KwestieTransition.Resolve, KwestieStatus.InProgress, KwestieStatus.Resolved)]
    public async Task HandleAsync_AllowedTransition_UsesDomainClockAndForwardsIdentifiersAndCancellation(
        WorkspaceRole role, KwestieTransition transition, KwestieStatus initial, KwestieStatus expected)
    {
        var entity = CreateEntity(initial);
        var repository = new RecordingRepository { Entity = entity };
        var memberships = new MembershipRepository { Role = role };
        var handler = new TransitionKwestieHandler(repository, memberships, new FixedClock());
        var command = Command(entity, transition);
        using var cancellation = new CancellationTokenSource();
        var previousResolved = entity.ResolvedAt;

        Assert.Equal(TransitionKwestieResult.Applied, await handler.HandleAsync(command, cancellation.Token));

        Assert.Equal(expected, entity.Status);
        Assert.Equal(Created, entity.CreatedAt);
        Assert.Equal(Now, entity.UpdatedAt);
        Assert.Equal(transition == KwestieTransition.Resolve ? Now : previousResolved, entity.ResolvedAt);
        Assert.Equal(transition == KwestieTransition.Close ? Now : (DateTimeOffset?)null, entity.ClosedAt);
        Assert.Equal((entity.WorkspaceId, command.UserId, cancellation.Token), memberships.Received);
        Assert.Equal((entity.WorkspaceId, entity.Id, cancellation.Token), repository.Received);
        Assert.Equal(cancellation.Token, repository.SaveToken);
        Assert.Same(entity, repository.Saved);
        Assert.Equal(1, memberships.Calls);
        Assert.Equal(1, repository.Reads);
        Assert.Equal(1, repository.Writes);
    }

    [Fact]
    public async Task HandleAsync_NoActiveMembership_ReturnsNotFoundWithoutLoadingOrSaving()
    {
        var entity = CreateEntity();
        var before = State(entity);
        var repository = new RecordingRepository { Entity = entity };
        // These persisted membership outcomes are intentionally indistinguishable to Application.
        var memberships = new MembershipRepository { Role = null };
        var handler = new TransitionKwestieHandler(repository, memberships, new FixedClock());

        Assert.Equal(TransitionKwestieResult.NotFound, await handler.HandleAsync(Command(entity)));
        Assert.Equal(before, State(entity));
        Assert.Equal(0, repository.Reads);
        Assert.Equal(0, repository.Writes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HandleAsync_MissingOrForeignKwestie_ReturnsNotFoundBeforeRoleRejection(bool foreign)
    {
        var entity = CreateEntity();
        var repository = new RecordingRepository { Entity = foreign ? entity : null };
        var memberships = new MembershipRepository { Role = WorkspaceRole.Member };
        var command = Command(entity, KwestieTransition.Close) with { WorkspaceId = Guid.NewGuid() };
        var handler = new TransitionKwestieHandler(repository, memberships, new FixedClock());

        Assert.Equal(TransitionKwestieResult.NotFound, await handler.HandleAsync(command));
        Assert.Equal(1, repository.Reads);
        Assert.Equal((command.WorkspaceId, command.KwestieId, CancellationToken.None), repository.Received);
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task HandleAsync_MemberCannotClose_LeavesStateAndTimestampsUntouched()
    {
        var entity = CreateEntity(KwestieStatus.Resolved);
        var before = State(entity);
        var repository = new RecordingRepository { Entity = entity };
        var handler = new TransitionKwestieHandler(repository, new MembershipRepository { Role = WorkspaceRole.Member }, new FixedClock());

        Assert.Equal(TransitionKwestieResult.Forbidden, await handler.HandleAsync(Command(entity, KwestieTransition.Close)));
        Assert.Equal(before, State(entity));
        Assert.Equal(0, repository.Writes);
    }

    [Theory]
    [InlineData(KwestieStatus.Open, KwestieTransition.Resolve)]
    [InlineData(KwestieStatus.Open, KwestieTransition.Close)]
    [InlineData(KwestieStatus.InProgress, KwestieTransition.StartProgress)]
    [InlineData(KwestieStatus.InProgress, KwestieTransition.Close)]
    [InlineData(KwestieStatus.Resolved, KwestieTransition.StartProgress)]
    [InlineData(KwestieStatus.Resolved, KwestieTransition.Resolve)]
    [InlineData(KwestieStatus.Closed, KwestieTransition.StartProgress)]
    [InlineData(KwestieStatus.Closed, KwestieTransition.Resolve)]
    [InlineData(KwestieStatus.Closed, KwestieTransition.Close)]
    public async Task HandleAsync_InvalidState_ReturnsConflictWithoutSavingOrMutating(
        KwestieStatus state, KwestieTransition transition)
    {
        var entity = CreateEntity(state);
        var before = State(entity);
        var repository = new RecordingRepository { Entity = entity };
        var handler = new TransitionKwestieHandler(repository, new MembershipRepository(), new FixedClock());

        Assert.Equal(TransitionKwestieResult.Conflict, await handler.HandleAsync(Command(entity, transition)));
        Assert.Equal(before, State(entity));
        Assert.Equal(0, repository.Writes);
    }

    [Fact]
    public async Task HandleAsync_WaitsForRoleBeforeLoading()
    {
        var ready = new TaskCompletionSource<WorkspaceRole?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingRepository { Entity = CreateEntity() };
        var handler = new TransitionKwestieHandler(repository, new MembershipRepository { Completion = ready.Task }, new FixedClock());
        var pending = handler.HandleAsync(Command(repository.Entity));
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, repository.Reads);
        ready.SetResult(null);
        Assert.Equal(TransitionKwestieResult.NotFound, await pending);
    }

    [Fact]
    public async Task HandleAsync_WaitsForEntityBeforeMutating()
    {
        var entity = CreateEntity();
        var ready = new TaskCompletionSource<KwestieEntity?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingRepository { ReadCompletion = ready.Task };
        var handler = new TransitionKwestieHandler(repository, new MembershipRepository(), new FixedClock());
        var pending = handler.HandleAsync(Command(entity));
        Assert.False(pending.IsCompleted);
        Assert.Equal(KwestieStatus.Open, entity.Status);
        Assert.Equal(0, repository.Writes);
        ready.SetResult(entity);
        Assert.Equal(TransitionKwestieResult.Applied, await pending);
    }

    [Theory]
    [InlineData(true, TransitionKwestieResult.Applied)]
    [InlineData(false, TransitionKwestieResult.Conflict)]
    public async Task HandleAsync_WaitsForSaveAndMapsConcurrency(bool saved, TransitionKwestieResult expected)
    {
        var entity = CreateEntity();
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingRepository { Entity = entity, SaveCompletion = ready.Task };
        var handler = new TransitionKwestieHandler(repository, new MembershipRepository(), new FixedClock());
        var pending = handler.HandleAsync(Command(entity));
        Assert.False(pending.IsCompleted);
        Assert.Equal(1, repository.Writes);
        ready.SetResult(saved);
        Assert.Equal(expected, await pending);
    }

    [Theory]
    [InlineData("role")]
    [InlineData("read")]
    [InlineData("save")]
    public async Task HandleAsync_PropagatesCancellationWithoutConvertingItToConflict(string stage)
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var entity = CreateEntity();
        var repository = new RecordingRepository
        {
            Entity = entity,
            ReadCompletion = stage == "read" ? Task.FromCanceled<KwestieEntity?>(cancellation.Token) : null,
            SaveCompletion = stage == "save" ? Task.FromCanceled<bool>(cancellation.Token) : Task.FromResult(true)
        };
        var memberships = new MembershipRepository
        {
            Completion = stage == "role" ? Task.FromCanceled<WorkspaceRole?>(cancellation.Token) : null
        };
        var handler = new TransitionKwestieHandler(repository, memberships, new FixedClock());
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => handler.HandleAsync(Command(entity), cancellation.Token));
        Assert.Equal(stage == "save" ? 1 : 0, repository.Writes);
        Assert.Equal(cancellation.Token, memberships.Received.Token);
    }

    private static TransitionKwestieCommand Command(KwestieEntity entity, KwestieTransition transition = KwestieTransition.StartProgress) =>
        new(entity.WorkspaceId, entity.Id, Guid.NewGuid(), transition);

    private static (KwestieStatus, DateTimeOffset, DateTimeOffset?, DateTimeOffset?) State(KwestieEntity entity) =>
        (entity.Status, entity.UpdatedAt, entity.ResolvedAt, entity.ClosedAt);

    private static KwestieEntity CreateEntity(KwestieStatus state = KwestieStatus.Open)
    {
        var entity = new KwestieEntity(Guid.NewGuid(), Guid.NewGuid(), "Printer", "Cannot print", KwestiePriority.Normal, Guid.NewGuid(), Created);
        if (state >= KwestieStatus.InProgress) entity.StartProgress(Created.AddHours(1));
        if (state >= KwestieStatus.Resolved) entity.Resolve(Created.AddHours(2));
        if (state == KwestieStatus.Closed) entity.Close(Created.AddHours(3));
        return entity;
    }

    private sealed class FixedClock : TimeProvider { public override DateTimeOffset GetUtcNow() => Now; }

    private sealed class MembershipRepository : IWorkspaceRepository
    {
        public WorkspaceRole? Role { get; init; } = WorkspaceRole.Admin;
        public Task<WorkspaceRole?>? Completion { get; init; }
        public int Calls { get; private set; }
        public (Guid Workspace, Guid User, CancellationToken Token) Received { get; private set; }
        public Task<WorkspaceRole?> GetActiveRoleAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
        {
            Calls++;
            Received = (workspaceId, userId, cancellationToken);
            return Completion ?? Task.FromResult(Role);
        }
        public Task AddAsync(Workspace workspace, WorkspaceMember member, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<bool> HasActiveMembershipAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<Workspace?> GetForUserAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class RecordingRepository : IKwestieRepository
    {
        public KwestieEntity? Entity { get; init; }
        public Task<KwestieEntity?>? ReadCompletion { get; init; }
        public Task<bool> SaveCompletion { get; init; } = Task.FromResult(true);
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public (Guid Workspace, Guid Kwestie, CancellationToken Token) Received { get; private set; }
        public CancellationToken SaveToken { get; private set; }
        public KwestieEntity? Saved { get; private set; }
        public Task<KwestieEntity?> GetForUpdateAsync(Guid workspaceId, Guid kwestieId, CancellationToken cancellationToken = default)
        {
            Reads++;
            Received = (workspaceId, kwestieId, cancellationToken);
            return ReadCompletion ?? Task.FromResult(Entity?.WorkspaceId == workspaceId && Entity.Id == kwestieId ? Entity : null);
        }
        public Task<bool> UpdateAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default)
        {
            Writes++;
            Saved = kwestie;
            SaveToken = cancellationToken;
            return SaveCompletion;
        }
        public Task AddAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<KwestieDetails?> GetForWorkspaceAsync(Guid workspaceId, Guid kwestieId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<KwestieSummary>> ListForWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }
}
