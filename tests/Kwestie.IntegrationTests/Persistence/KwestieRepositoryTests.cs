using Kwestie.Domain.Kwesties;
using Kwestie.Infrastructure.Persistence;
using Kwestie.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit.Abstractions;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.IntegrationTests.Persistence;

public class KwestieRepositoryTests(ITestOutputHelper output)
{
    [Fact]
    public async Task AddAsync_PersistsKwestieAndReadsGeneratedNumberFromSqlServer()
    {
        using var configuration = new ConfigurationManager();
        configuration.AddUserSecrets<KwestieRepositoryTests>(optional: true);
        var connectionString = configuration.GetConnectionString("Kwestie");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "ConnectionStrings:Kwestie is missing from the API's shared .NET User Secrets. " +
                "This test requires the existing local Kwestie database with InitialCreate applied.");
        }

        var options = new DbContextOptionsBuilder<KwestieDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        var id = Guid.NewGuid();
        var createdAt = DateTimeOffset.UtcNow;
        var kwestie = new KwestieEntity(
            id, Guid.NewGuid(), $"Repository round-trip {id:N}",
            "SQL Server integration test", KwestiePriority.High,
            Guid.NewGuid(), createdAt);

        Assert.Equal(0L, kwestie.Number);
        output.WriteLine("Number before SaveChangesAsync: {0}", kwestie.Number);

        try
        {
            await using (var writeContext = new KwestieDbContext(options))
            {
                var repository = new KwestieRepository(writeContext);
                await repository.AddAsync(kwestie);
            }

            output.WriteLine("Number after SaveChangesAsync: {0}", kwestie.Number);
            Assert.True(kwestie.Number > 0);
            Assert.Equal(id, kwestie.Id);

            await using var readContext = new KwestieDbContext(options);
            var restored = await readContext.Kwesties.AsNoTracking().SingleAsync(k => k.Id == id);

            Assert.NotSame(kwestie, restored);
            Assert.Equal(id, restored.Id);
            Assert.Equal(kwestie.Number, restored.Number);
            Assert.Equal(kwestie.Title, restored.Title);
            Assert.Equal(kwestie.Description, restored.Description);
            Assert.Equal(kwestie.WorkspaceId, restored.WorkspaceId);
            Assert.Equal(kwestie.Priority, restored.Priority);
            Assert.Equal(KwestieStatus.Open, restored.Status);
            Assert.Equal(kwestie.CreatedById, restored.CreatedById);
            Assert.Equal(createdAt, restored.CreatedAt);
            Assert.Equal(createdAt, restored.UpdatedAt);
            output.WriteLine("Round-trip verified using a separate DbContext.");
        }
        finally
        {
            await using var cleanupContext = new KwestieDbContext(options);
            await cleanupContext.Kwesties.Where(k => k.Id == id).ExecuteDeleteAsync();
            Assert.False(await cleanupContext.Kwesties.AnyAsync(k => k.Id == id));
            output.WriteLine("Test record cleanup verified.");
        }
    }
}
