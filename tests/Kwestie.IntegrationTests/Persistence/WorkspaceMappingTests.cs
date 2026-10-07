using Kwestie.Domain.Workspaces;
using Kwestie.Infrastructure.Identity;
using Kwestie.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Storage;

namespace Kwestie.IntegrationTests.Persistence;

public class WorkspaceMappingTests
{
    [Fact]
    public void Model_MapsWorkspaceWithApplicationGeneratedIdAndRequiredProperties()
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Workspace))!;

        Assert.Equal("Workspaces", entity.GetTableName());
        Assert.Equal(nameof(Workspace.Id), Assert.Single(entity.FindPrimaryKey()!.Properties).Name);
        Assert.Equal(ValueGenerated.Never, entity.FindProperty(nameof(Workspace.Id))!.ValueGenerated);
        Assert.Null(entity.FindProperty(nameof(Workspace.Name))!.GetMaxLength());
        Assert.All(entity.GetProperties(), property => Assert.False(property.IsNullable));
        Assert.Empty(entity.GetNavigations());
    }

    [Fact]
    public void Model_MapsCompositeMembershipKeyAndExpectedForeignKeys()
    {
        using var context = CreateContext();
        var entity = context.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(WorkspaceMember))!;

        Assert.Equal("WorkspaceMembers", entity.GetTableName());
        Assert.Equal(new[] { nameof(WorkspaceMember.WorkspaceId), nameof(WorkspaceMember.UserId) },
            entity.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.All(entity.GetProperties(), property => Assert.False(property.IsNullable));
        Assert.All(entity.FindPrimaryKey()!.Properties,
            property => Assert.Equal(ValueGenerated.Never, property.ValueGenerated));
        Assert.Equal("int", entity.FindProperty(nameof(WorkspaceMember.Role))!.GetColumnType());
        Assert.Equal(2, entity.GetForeignKeys().Count());

        var workspaceFk = Assert.Single(entity.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType == typeof(Workspace));
        Assert.Equal(nameof(WorkspaceMember.WorkspaceId), Assert.Single(workspaceFk.Properties).Name);
        Assert.Equal(nameof(Workspace.Id), Assert.Single(workspaceFk.PrincipalKey.Properties).Name);
        Assert.Equal(DeleteBehavior.Cascade, workspaceFk.DeleteBehavior);
        var userFk = Assert.Single(entity.GetForeignKeys(),
            fk => fk.PrincipalEntityType.ClrType == typeof(ApplicationUser));
        Assert.Equal(nameof(WorkspaceMember.UserId), Assert.Single(userFk.Properties).Name);
        Assert.Equal("AspNetUsers", userFk.PrincipalEntityType.GetTableName());
        Assert.Equal(nameof(ApplicationUser.Id), Assert.Single(userFk.PrincipalKey.Properties).Name);
        Assert.Equal(DeleteBehavior.NoAction, userFk.DeleteBehavior);
        Assert.Empty(entity.GetNavigations());
    }

    [Fact]
    public void Materializer_RestoresWorkspaceWithoutChangingDomain()
    {
        using var context = CreateContext();
        AssertMaterialized<Workspace>(context, new Dictionary<string, object?>
        {
            [nameof(Workspace.Id)] = Guid.NewGuid(),
            [nameof(Workspace.Name)] = "Support",
            [nameof(Workspace.CreatedAt)] = new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.FromHours(-6))
        });
    }

    [Fact]
    public void Materializer_RestoresMembershipIncludingStoredInactiveState()
    {
        using var context = CreateContext();
        AssertMaterialized<WorkspaceMember>(context, new Dictionary<string, object?>
        {
            [nameof(WorkspaceMember.WorkspaceId)] = Guid.NewGuid(),
            [nameof(WorkspaceMember.UserId)] = Guid.NewGuid(),
            [nameof(WorkspaceMember.Role)] = WorkspaceRole.Member,
            [nameof(WorkspaceMember.JoinedAt)] = new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.FromHours(-6)),
            [nameof(WorkspaceMember.IsActive)] = false
        });
    }

    [Fact]
    public void Model_HasNoPendingChangesAfterAddWorkspacesMigration()
    {
        using var context = CreateContext();
        Assert.False(context.Database.HasPendingModelChanges());
    }

    private static void AssertMaterialized<T>(KwestieDbContext context, Dictionary<string, object?> expected)
    {
        var entity = context.Model.FindEntityType(typeof(T))!;
        var values = new object[entity.GetProperties().Count()];
        foreach (var property in entity.GetProperties())
            values[property.GetIndex()] = expected[property.Name]!;

        var materializer = context.GetService<IStructuralTypeMaterializerSource>().GetMaterializer(entity);
        var restored = Assert.IsType<T>(materializer(new MaterializationContext(new ValueBuffer(values), context)));
        foreach (var property in entity.GetProperties())
            Assert.Equal(expected[property.Name], property.PropertyInfo!.GetValue(restored));
    }

    private static KwestieDbContext CreateContext() => new(
        new DbContextOptionsBuilder<KwestieDbContext>()
            .UseSqlServer("Server=unused;Database=WorkspaceMappingTests;Integrated Security=true")
            .Options);
}
