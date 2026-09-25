using Kwestie.Infrastructure;
using Kwestie.Infrastructure.Identity;
using Kwestie.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.IntegrationTests.Persistence;

public class IdentityInfrastructureTests
{
    private const string ModelConnectionString =
        "Server=unused;Database=IdentityModelTests;Integrated Security=true";

    [Fact]
    public void Model_ContainsGuidUsersAndUserTablesWithoutGlobalRoles()
    {
        using var context = new KwestieDbContext(
            new DbContextOptionsBuilder<KwestieDbContext>().UseSqlServer(ModelConnectionString).Options);
        var model = context.Model;
        var user = model.FindEntityType(typeof(ApplicationUser))!;

        Assert.Equal("AspNetUsers", user.GetTableName());
        var key = Assert.Single(user.FindPrimaryKey()!.Properties);
        Assert.Equal(nameof(ApplicationUser.Id), key.Name);
        Assert.Equal(typeof(Guid), key.ClrType);

        Assert.Equal("AspNetUserClaims", model.FindEntityType(typeof(IdentityUserClaim<Guid>))!.GetTableName());
        Assert.Equal("AspNetUserLogins", model.FindEntityType(typeof(IdentityUserLogin<Guid>))!.GetTableName());
        Assert.Equal("AspNetUserTokens", model.FindEntityType(typeof(IdentityUserToken<Guid>))!.GetTableName());
        Assert.Null(model.FindEntityType(typeof(IdentityRole<Guid>)));
        Assert.Null(model.FindEntityType(typeof(IdentityUserRole<Guid>)));

        var kwestie = model.FindEntityType(typeof(KwestieEntity))!;
        Assert.Equal("Kwesties", kwestie.GetTableName());
        Assert.Empty(kwestie.GetForeignKeys());
        Assert.Empty(kwestie.GetNavigations());
        Assert.Equal(typeof(Guid), kwestie.FindProperty(nameof(KwestieEntity.CreatedById))!.ClrType);
        Assert.Equal(typeof(Guid?), kwestie.FindProperty(nameof(KwestieEntity.AssignedToId))!.ClrType);
    }

    [Fact]
    public void AddInfrastructure_ResolvesUserManagerAndEfStoreWithoutRoleSupport()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure(ModelConnectionString);
        using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
        using var scope = provider.CreateScope();

        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var store = scope.ServiceProvider.GetRequiredService<IUserStore<ApplicationUser>>();

        Assert.IsAssignableFrom<IUserPasswordStore<ApplicationUser>>(store);
        var efStore = Assert.IsAssignableFrom<UserOnlyStore<ApplicationUser, KwestieDbContext, Guid,
            IdentityUserClaim<Guid>, IdentityUserLogin<Guid>, IdentityUserToken<Guid>>>(store);
        Assert.Same(scope.ServiceProvider.GetRequiredService<KwestieDbContext>(), efStore.Context);
        Assert.False(manager.SupportsUserRole);
        Assert.Null(scope.ServiceProvider.GetService<RoleManager<IdentityRole<Guid>>>());
    }
}
