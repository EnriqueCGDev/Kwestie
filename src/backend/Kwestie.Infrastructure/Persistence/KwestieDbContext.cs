using Kwestie.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Infrastructure.Persistence;

public class KwestieDbContext(DbContextOptions<KwestieDbContext> options) : DbContext(options)
{
    public DbSet<KwestieEntity> Kwesties => Set<KwestieEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfiguration(new KwestieConfiguration());
    }
}
