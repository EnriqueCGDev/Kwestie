using Kwestie.Application.Authentication.Register;
using Kwestie.Application.Workspaces;
using Kwestie.Domain.Workspaces;
using Kwestie.Infrastructure;
using Kwestie.Infrastructure.Persistence;
using Kwestie.Infrastructure.Persistence.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Kwestie.IntegrationTests.Persistence;

public class WorkspaceRepositorySqlTests
{
    [Fact]
    public async Task AddAsync_PersistsWorkspaceAndMembershipAndEnforcesUserDeletionRestriction()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(connectionString).Options;
        var services = new ServiceCollection();
        services.AddInfrastructure(connectionString);
        await using var provider = services.BuildServiceProvider();
        var email = $"Workspace-{Guid.NewGuid():N}@example.com";
        var workspaceId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var workspace = new Workspace(workspaceId, $"  Support {workspaceId:N}  ", createdAt);

        try
        {
            await using var writeScope = provider.CreateAsyncScope();
            var registration = await writeScope.ServiceProvider.GetRequiredService<IUserRegistration>()
                .RegisterAsync(email, $"Test-{Guid.NewGuid():N}-Aa1!");
            Assert.True(registration.Succeeded);
            var userId = registration.UserId!.Value;
            var member = new WorkspaceMember(workspaceId, userId, WorkspaceRole.Admin, createdAt);

            await writeScope.ServiceProvider.GetRequiredService<IWorkspaceRepository>()
                .AddAsync(workspace, member);

            await using var readContext = new KwestieDbContext(options);
            var storedWorkspace = await readContext.Workspaces.AsNoTracking().SingleAsync(w => w.Id == workspaceId);
            var storedMember = await readContext.WorkspaceMembers.AsNoTracking()
                .SingleAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
            Assert.NotSame(workspace, storedWorkspace);
            Assert.NotSame(member, storedMember);
            Assert.Equal(workspace.Id, storedWorkspace.Id);
            Assert.Equal(workspace.Name, storedWorkspace.Name);
            Assert.Equal(createdAt, storedWorkspace.CreatedAt);
            Assert.Equal(workspaceId, storedMember.WorkspaceId);
            Assert.Equal(userId, storedMember.UserId);
            Assert.Equal(WorkspaceRole.Admin, storedMember.Role);
            Assert.Equal(createdAt, storedMember.JoinedAt);
            Assert.True(storedMember.IsActive);

            // The membership FK must prevent silently deleting this user's memberships.
            var user = await readContext.Users.SingleAsync(u => u.Id == userId);
            readContext.Users.Remove(user);
            var failure = await Assert.ThrowsAsync<DbUpdateException>(() => readContext.SaveChangesAsync());
            Assert.Equal(547, Assert.IsType<SqlException>(failure.InnerException).Number);
            Assert.True(await readContext.Users.AsNoTracking().AnyAsync(u => u.Id == userId));
            Assert.True(await readContext.WorkspaceMembers.AsNoTracking()
                .AnyAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId));
        }
        finally
        {
            await using var cleanupContext = new KwestieDbContext(options);
            await cleanupContext.Workspaces.Where(w => w.Id == workspaceId).ExecuteDeleteAsync();
            await cleanupContext.Users.Where(u => u.Email == email).ExecuteDeleteAsync();
            Assert.False(await cleanupContext.Workspaces.AnyAsync(w => w.Id == workspaceId));
            // Workspace deletion must cascade to its membership before deleting the test user.
            Assert.False(await cleanupContext.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == workspaceId));
            Assert.False(await cleanupContext.Users.AnyAsync(u => u.Email == email));
        }
    }

    [Fact]
    public async Task AddAsync_MembershipWithMissingUser_RollsBackWorkspaceInsert()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(connectionString).Options;
        var workspaceId = Guid.NewGuid();
        var missingUserId = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var workspace = new Workspace(workspaceId, $"Atomicity {workspaceId:N}", createdAt);
        var member = new WorkspaceMember(workspaceId, missingUserId, WorkspaceRole.Admin, createdAt);

        try
        {
            await using (var writeContext = new KwestieDbContext(options))
            {
                Assert.False(await writeContext.Users.AnyAsync(u => u.Id == missingUserId));
                var repository = new WorkspaceRepository(writeContext);
                var failure = await Assert.ThrowsAsync<DbUpdateException>(() => repository.AddAsync(workspace, member));
                Assert.Equal(547, Assert.IsType<SqlException>(failure.InnerException).Number);
            }

            await using var readContext = new KwestieDbContext(options);
            Assert.False(await readContext.Workspaces.AnyAsync(w => w.Id == workspaceId));
            Assert.False(await readContext.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == workspaceId));
        }
        finally
        {
            await using var cleanupContext = new KwestieDbContext(options);
            await cleanupContext.Workspaces.Where(w => w.Id == workspaceId).ExecuteDeleteAsync();
            Assert.False(await cleanupContext.Workspaces.AnyAsync(w => w.Id == workspaceId));
            Assert.False(await cleanupContext.WorkspaceMembers.AnyAsync(m => m.WorkspaceId == workspaceId));
        }
    }

    private static async Task<string> GetConnectionStringAsync()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddUserSecrets<WorkspaceRepositorySqlTests>(optional: true);
        var connectionString = configuration.GetConnectionString("Kwestie");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Kwestie is required in shared API User Secrets; AddWorkspaces must already be applied manually.");

        await using var context = new KwestieDbContext(
            new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(connectionString).Options);
        Assert.False(context.Database.HasPendingModelChanges());
        var migrations = await context.Database.GetAppliedMigrationsAsync();
        if (!migrations.Any(name => name.EndsWith("_AddWorkspaces", StringComparison.Ordinal)))
            throw new InvalidOperationException("AddWorkspaces is not applied to the local Kwestie database. Apply it manually before running Workspace SQL tests; tests do not apply migrations.");

        return connectionString;
    }
}
