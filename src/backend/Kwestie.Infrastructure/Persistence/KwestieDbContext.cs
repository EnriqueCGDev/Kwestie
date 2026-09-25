using Kwestie.Infrastructure.Persistence.Configurations;
using Kwestie.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Infrastructure.Persistence;

public class KwestieDbContext(DbContextOptions<KwestieDbContext> options)
    : IdentityUserContext<ApplicationUser, Guid>(options)
{
    public DbSet<KwestieEntity> Kwesties => Set<KwestieEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new KwestieConfiguration());
    }
}
