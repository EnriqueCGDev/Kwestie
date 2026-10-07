using Kwestie.Infrastructure.Persistence.Configurations;
using Kwestie.Infrastructure.Identity;
using Kwestie.Infrastructure.Authentication;
using Kwestie.Domain.Workspaces;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Infrastructure.Persistence;

public class KwestieDbContext(DbContextOptions<KwestieDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<KwestieEntity> Kwesties => Set<KwestieEntity>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new KwestieConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
        modelBuilder.ApplyConfiguration(new WorkspaceConfiguration());
        modelBuilder.ApplyConfiguration(new WorkspaceMemberConfiguration());
    }
}
