using Kwestie.Application.Kwesties;
using Kwestie.Application.Kwesties.Create;
using Kwestie.Domain.Common;
using Kwestie.Domain.Kwesties;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Tests.Kwesties.Create;

public class CreateKwestieHandlerTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 9, 24, 12, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task HandleAsync_ValidCommand_AddsEntityAndReturnsItsId()
    {
        var repository = new RecordingRepository();
        var handler = new CreateKwestieHandler(repository, new FixedTimeProvider(Now));
        var command = CreateCommand();
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
    }

    [Fact]
    public async Task HandleAsync_InvalidTitle_PropagatesDomainExceptionWithoutCallingRepository()
    {
        var repository = new RecordingRepository();
        var handler = new CreateKwestieHandler(repository, new FixedTimeProvider(Now));
        var command = CreateCommand() with { Title = "   " };

        await Assert.ThrowsAsync<DomainException>(() => handler.HandleAsync(command));

        Assert.Equal(0, repository.AddCalls);
        Assert.Null(repository.AddedKwestie);
    }

    [Fact]
    public async Task HandleAsync_WaitsForRepositoryBeforeReturningResult()
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var repository = new RecordingRepository { Completion = completion.Task };
        var handler = new CreateKwestieHandler(repository, new FixedTimeProvider(Now));

        var pendingResult = handler.HandleAsync(CreateCommand());

        Assert.Equal(1, repository.AddCalls);
        Assert.False(pendingResult.IsCompleted);
        completion.SetResult();
        var result = await pendingResult;
        Assert.Equal(repository.AddedKwestie!.Id, result.Id);
    }

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
}
