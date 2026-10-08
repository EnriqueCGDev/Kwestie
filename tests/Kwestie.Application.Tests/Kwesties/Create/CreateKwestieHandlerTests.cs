using Kwestie.Application.Kwesties;
using Kwestie.Application.Kwesties.Create;
using Kwestie.Application.Workspaces;
using Kwestie.Domain.Common;
using Kwestie.Domain.Kwesties;
using Kwestie.Domain.Workspaces;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Tests.Kwesties.Create;

public class CreateKwestieHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 24, 12, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    public async Task HandleAsync_ActiveMember_AddsEntityAndReturnsItsId(WorkspaceRole role)
    {
        var repository = new RecordingRepository();
        var command = CreateCommand();
        var workspaceRepository = AccessibleWorkspace(command, role);
        var handler = new CreateKwestieHandler(repository, workspaceRepository, new FixedTimeProvider(Now));
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(command, cancellation.Token);

        Assert.Equal(1, repository.AddCalls);
        var kwestie = Assert.IsType<KwestieEntity>(repository.AddedKwestie);
        Assert.Equal(command.WorkspaceId, kwestie.WorkspaceId);
        Assert.Equal(command.Title.Trim(), kwestie.Title);
        Assert.Equal(command.Description!.Trim(), kwestie.Description);
        Assert.Equal(command.Priority, kwestie.Priority);
        Assert.Equal(command.CreatedById, kwestie.CreatedById);
        Assert.Equal(command.CategoryId, kwestie.CategoryId);
        Assert.NotEqual(Guid.Empty, kwestie.Id);
        Assert.Equal(kwestie.Id, result.Id);
        Assert.Equal(Now, kwestie.CreatedAt);
        Assert.Equal(kwestie.CreatedAt, kwestie.UpdatedAt);
        Assert.Equal(0L, kwestie.Number);
        Assert.Equal(KwestieStatus.Open, kwestie.Status);
        Assert.Equal(cancellation.Token, repository.ReceivedCancellationToken);
        Assert.Equal(1, workspaceRepository.CheckCalls);
        Assert.Equal(command.WorkspaceId, workspaceRepository.ReceivedWorkspaceId);
        Assert.Equal(command.CreatedById, workspaceRepository.ReceivedUserId);
        Assert.Equal(cancellation.Token, workspaceRepository.ReceivedCancellationToken);
    }

    [Fact]
    public async Task HandleAsync_InvalidTitle_PropagatesDomainExceptionWithoutCallingRepository()
    {
        var repository = new RecordingRepository();
        var command = CreateCommand() with { Title = "   " };
        var workspaceRepository = AccessibleWorkspace(command);
        var handler = new CreateKwestieHandler(repository, workspaceRepository, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));

        Assert.Equal(0, repository.AddCalls);
        Assert.Null(repository.AddedKwestie);
        Assert.Equal(0, workspaceRepository.CheckCalls);
    }

    [Fact]
    public async Task HandleAsync_WaitsForRepositoryBeforeReturningResult()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingRepository { Completion = completion.Task };
        var command = CreateCommand();
        var handler = new CreateKwestieHandler(repository, AccessibleWorkspace(command), new FixedTimeProvider(Now));

        var pendingResult = handler.HandleAsync(command);

        Assert.Equal(1, repository.AddCalls);
        Assert.False(pendingResult.IsCompleted);
        completion.SetResult();
        var result = await pendingResult;
        Assert.Equal(repository.AddedKwestie!.Id, result.Id);
    }

    [Theory]
    [InlineData("MissingWorkspace")]
    [InlineData("OtherUser")]
    [InlineData("InactiveMembership")]
    public async Task HandleAsync_WithoutActiveMembership_RejectsWithoutPersisting(string scenario)
    {
        var command = CreateCommand();
        var repository = new RecordingRepository();
        var workspaceRepository = new RecordingWorkspaceRepository
        {
            WorkspaceId = scenario == "MissingWorkspace" ? null : command.WorkspaceId,
            Member = new WorkspaceMember(command.WorkspaceId,
                scenario == "OtherUser" ? Guid.NewGuid() : command.CreatedById, WorkspaceRole.Admin, Now),
            IsActive = scenario != "InactiveMembership"
        };
        var handler = new CreateKwestieHandler(repository, workspaceRepository, new FixedTimeProvider(Now));

        await Assert.ThrowsAsync<CreateKwestieAccessDeniedException>(() => handler.HandleAsync(command));

        Assert.Equal(1, workspaceRepository.CheckCalls);
        Assert.Equal(0, repository.AddCalls);
        Assert.Null(repository.AddedKwestie);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task HandleAsync_WaitsForAccessCheckBeforePersisting(bool allowed)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var command = CreateCommand();
        var repository = new RecordingRepository();
        var workspaceRepository = new RecordingWorkspaceRepository { Completion = completion.Task };
        var handler = new CreateKwestieHandler(repository, workspaceRepository, new FixedTimeProvider(Now));

        var pending = handler.HandleAsync(command);

        Assert.Equal(1, workspaceRepository.CheckCalls);
        Assert.False(pending.IsCompleted);
        Assert.Equal(0, repository.AddCalls);
        completion.SetResult(allowed);
        if (allowed)
        {
            var result = await pending;
            Assert.Equal(1, repository.AddCalls);
            Assert.Equal(repository.AddedKwestie!.Id, result.Id);
        }
        else
        {
            await Assert.ThrowsAsync<CreateKwestieAccessDeniedException>(() => pending);
            Assert.Equal(0, repository.AddCalls);
        }
    }

    [Fact]
    public async Task HandleAsync_AccessCheckFailure_PropagatesWithoutPersisting()
    {
        var failure = new InvalidOperationException("Access query failed.");
        var repository = new RecordingRepository();
        var workspaceRepository = new RecordingWorkspaceRepository { Completion = Task.FromException<bool>(failure) };
        var handler = new CreateKwestieHandler(repository, workspaceRepository, new FixedTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(CreateCommand()));

        Assert.Same(failure, exception);
        Assert.Equal(0, repository.AddCalls);
    }

    private static RecordingWorkspaceRepository AccessibleWorkspace(
        CreateKwestieCommand command, WorkspaceRole role = WorkspaceRole.Admin) => new()
    {
        WorkspaceId = command.WorkspaceId,
        Member = new WorkspaceMember(command.WorkspaceId, command.CreatedById, role, Now)
    };

    private static CreateKwestieCommand CreateCommand() => new(
        Guid.NewGuid(), "  Broken printer  ", "  Cannot print.  ",
        KwestiePriority.High, Guid.NewGuid(), Guid.NewGuid());

    private sealed class RecordingRepository : IKwestieRepository
    {
        public int AddCalls { get; private set; }
        public KwestieEntity? AddedKwestie { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }
        public Task Completion { get; init; } = Task.CompletedTask;

        public Task AddAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default)
        {
            AddCalls++;
            AddedKwestie = kwestie;
            ReceivedCancellationToken = cancellationToken;
            return Completion;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private sealed class RecordingWorkspaceRepository : IWorkspaceRepository
    {
        public Guid? WorkspaceId { get; init; }
        public WorkspaceMember? Member { get; init; }
        // Represents persisted activity without adding membership-changing methods to Domain.
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
    }
}
