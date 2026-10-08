using Kwestie.Application.Workspaces;
using Kwestie.Application.Workspaces.Create;
using Kwestie.Domain.Common;
using Kwestie.Domain.Workspaces;

namespace Kwestie.Application.Tests.Workspaces.Create;

public class CreateWorkspaceHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 6, 16, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_ValidCommand_AddsWorkspaceAndAdminMembershipAndReturnsWorkspaceId()
    {
        var repository = new RecordingRepository();
        var handler = new CreateWorkspaceHandler(repository, new FixedTimeProvider(Now));
        var command = CreateCommand();
        using var cancellation = new CancellationTokenSource();

        var result = await handler.HandleAsync(command, cancellation.Token);

        Assert.Equal(1, repository.AddCalls);
        var workspace = Assert.IsType<Workspace>(repository.AddedWorkspace);
        var member = Assert.IsType<WorkspaceMember>(repository.AddedMember);
        Assert.NotEqual(Guid.Empty, workspace.Id);
        Assert.Equal(workspace.Id, result.WorkspaceId);
        Assert.Equal(command.Name.Trim(), workspace.Name);
        Assert.Equal(Now, workspace.CreatedAt);
        Assert.Equal(workspace.Id, member.WorkspaceId);
        Assert.Equal(command.UserId, member.UserId);
        Assert.Equal(WorkspaceRole.Admin, member.Role);
        Assert.Equal(workspace.CreatedAt, member.JoinedAt);
        Assert.True(member.IsActive);
        Assert.Equal(cancellation.Token, repository.ReceivedCancellationToken);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public async Task HandleAsync_InvalidName_PropagatesDomainExceptionWithoutCallingRepository(string? name)
    {
        var repository = new RecordingRepository();
        var handler = new CreateWorkspaceHandler(repository, new FixedTimeProvider(Now));
        var command = CreateCommand() with { Name = name! };

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));

        Assert.Equal(0, repository.AddCalls);
        Assert.Null(repository.AddedWorkspace);
        Assert.Null(repository.AddedMember);
    }

    [Fact]
    public async Task HandleAsync_EmptyUserId_PropagatesDomainExceptionWithoutCallingRepository()
    {
        var repository = new RecordingRepository();
        var handler = new CreateWorkspaceHandler(repository, new FixedTimeProvider(Now));
        var command = CreateCommand() with { UserId = Guid.Empty };

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));

        Assert.Equal(0, repository.AddCalls);
        Assert.Null(repository.AddedWorkspace);
        Assert.Null(repository.AddedMember);
    }

    [Fact]
    public async Task HandleAsync_WaitsForRepositoryBeforeReturningResult()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingRepository { Completion = completion.Task };
        var handler = new CreateWorkspaceHandler(repository, new FixedTimeProvider(Now));

        var pendingResult = handler.HandleAsync(CreateCommand());

        Assert.Equal(1, repository.AddCalls);
        Assert.False(pendingResult.IsCompleted);
        completion.SetResult();
        var result = await pendingResult;
        Assert.Equal(repository.AddedWorkspace!.Id, result.WorkspaceId);
    }

    [Fact]
    public async Task HandleAsync_PersistenceFailure_PropagatesWithoutReturningResult()
    {
        var failure = new InvalidOperationException("Persistence failed.");
        var repository = new RecordingRepository { Completion = Task.FromException(failure) };
        var handler = new CreateWorkspaceHandler(repository, new FixedTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => handler.HandleAsync(CreateCommand()));

        Assert.Same(failure, exception);
        Assert.Equal(1, repository.AddCalls);
    }

    private static CreateWorkspaceCommand CreateCommand() => new("  Support team  ", Guid.NewGuid());

    private sealed class RecordingRepository : IWorkspaceRepository
    {
        public int AddCalls { get; private set; }
        public Workspace? AddedWorkspace { get; private set; }
        public WorkspaceMember? AddedMember { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }
        public Task Completion { get; init; } = Task.CompletedTask;

        public Task AddAsync(
            Workspace workspace,
            WorkspaceMember initialMember,
            CancellationToken cancellationToken = default)
        {
            AddCalls++;
            AddedWorkspace = workspace;
            AddedMember = initialMember;
            ReceivedCancellationToken = cancellationToken;
            return Completion;
        }

        public Task<IReadOnlyList<Workspace>> ListForUserAsync(
            Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> HasActiveMembershipAsync(
            Guid workspaceId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<Workspace?> GetForUserAsync(
            Guid workspaceId, Guid userId, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
