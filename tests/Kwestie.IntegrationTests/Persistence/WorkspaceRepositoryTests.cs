using Kwestie.Application.Workspaces;
using Kwestie.Domain.Workspaces;
using Kwestie.Infrastructure;
using Kwestie.Infrastructure.Persistence;
using Kwestie.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kwestie.IntegrationTests.Persistence;

public class WorkspaceRepositoryTests
{
    private const string ModelConnectionString =
        "Server=unused;Database=WorkspaceRepositoryTests;Integrated Security=true";

    [Fact]
    public void AddInfrastructure_ResolvesScopedWorkspaceRepository()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(ModelConnectionString);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();

        var repository = first.ServiceProvider.GetRequiredService<IWorkspaceRepository>();
        Assert.IsType<WorkspaceRepository>(repository);
        Assert.Same(repository, first.ServiceProvider.GetRequiredService<IWorkspaceRepository>());
        Assert.NotSame(repository, second.ServiceProvider.GetRequiredService<IWorkspaceRepository>());
    }

    [Fact]
    public async Task AddAsync_TracksBothEntitiesAndAwaitsOneSaveWithCallerCancellation()
    {
        var completion = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        await using var context = new RecordingContext { Completion = completion.Task };
        var repository = new WorkspaceRepository(context);
        var createdAt = DateTimeOffset.UtcNow;
        var workspace = new Workspace(Guid.NewGuid(), "Support", createdAt);
        var member = new WorkspaceMember(workspace.Id, Guid.NewGuid(), WorkspaceRole.Admin, createdAt);
        using var cancellation = new CancellationTokenSource();

        var pending = repository.AddAsync(workspace, member, cancellation.Token);

        Assert.False(pending.IsCompleted);
        Assert.Equal(1, context.SaveCalls);
        Assert.Equal(cancellation.Token, context.ReceivedCancellationToken);
        Assert.Equal(EntityState.Added, context.Entry(workspace).State);
        Assert.Equal(EntityState.Added, context.Entry(member).State);
        Assert.Equal(2, context.ChangeTracker.Entries().Count());
        completion.SetResult(2);
        await pending;
        Assert.Equal(1, context.SaveCalls);
    }

    private sealed class RecordingContext() : KwestieDbContext(
        new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(ModelConnectionString).Options)
    {
        public Task<int> Completion { get; init; } = Task.FromResult(2);
        public int SaveCalls { get; private set; }
        public CancellationToken ReceivedCancellationToken { get; private set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            SaveCalls++;
            ReceivedCancellationToken = cancellationToken;
            return Completion;
        }
    }
}
