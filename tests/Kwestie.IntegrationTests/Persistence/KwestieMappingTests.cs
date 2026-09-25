using Kwestie.Domain.Kwesties;
using Kwestie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.IntegrationTests.Persistence;

public class KwestieMappingTests
{
    private static KwestieDbContext CreateContext() => new(
        new DbContextOptionsBuilder<KwestieDbContext>()
            .UseSqlServer("Server=unused;Database=MappingTests;Integrated Security=true")
            .Options);

    [Fact]
    public void Model_UsesSqlServerIdentityAndApplicationGeneratedId()
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(KwestieEntity))!;
        Assert.Equal("Kwesties", entity.GetTableName());
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(KwestieEntity.Id))!.ValueGenerated);
        var number = entity.FindProperty(nameof(KwestieEntity.Number))!;
        Assert.Equal("bigint", number.GetColumnType());
        Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn, number.GetValueGenerationStrategy());
        Assert.Equal(1L, number.GetIdentitySeed());
        Assert.Equal(1, number.GetIdentityIncrement());
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique && index.Properties.SequenceEqual(new[] { number }));
        Assert.Empty(entity.GetForeignKeys());
        Assert.Null(entity.FindProperty(nameof(KwestieEntity.Title))!.GetMaxLength());
        Assert.Null(entity.FindProperty(nameof(KwestieEntity.Description))!.GetMaxLength());
    }

    [Fact]
    public void Materializer_RestoresPersistedStateWithoutChangingDomain()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(KwestieEntity))!;
        var createdAt = new DateTimeOffset(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
        var expected = new Dictionary<string, object?>
        {
            ["Id"] = Guid.NewGuid(), ["Number"] = 142L,
            ["WorkspaceId"] = Guid.NewGuid(), ["Title"] = "Printer",
            ["Description"] = "Repaired", ["Status"] = KwestieStatus.Closed,
            ["Priority"] = KwestiePriority.High, ["CreatedById"] = Guid.NewGuid(),
            ["AssignedToId"] = Guid.NewGuid(), ["CategoryId"] = Guid.NewGuid(),
            ["CreatedAt"] = createdAt, ["UpdatedAt"] = createdAt.AddHours(3),
            ["ResolvedAt"] = createdAt.AddHours(2), ["ClosedAt"] = createdAt.AddHours(3)
        };
        var values = new object[entity.GetProperties().Count()];
        foreach (var property in entity.GetProperties())
            values[property.GetIndex()] = expected[property.Name]!;

        // Exercise EF's actual materializer with a value buffer, without opening a connection.
        var materializer = context.GetService<IStructuralTypeMaterializerSource>().GetMaterializer(entity);
        var restored = Assert.IsType<KwestieEntity>(
            materializer(new MaterializationContext(new ValueBuffer(values), context)));

        foreach (var property in entity.GetProperties())
            Assert.Equal(expected[property.Name], property.PropertyInfo!.GetValue(restored));
    }
}
