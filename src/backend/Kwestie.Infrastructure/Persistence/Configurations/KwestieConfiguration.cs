using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Infrastructure.Persistence.Configurations;

public sealed class KwestieConfiguration : IEntityTypeConfiguration<KwestieEntity>
{
    public void Configure(EntityTypeBuilder<KwestieEntity> builder)
    {
        builder.ToTable("Kwesties");
        builder.HasKey(k => k.Id);
        builder.Property(k => k.Id).ValueGeneratedNever();
        builder.Property(k => k.Number).HasColumnType("bigint").UseIdentityColumn(1, 1);
        builder.HasIndex(k => k.Number).IsUnique();
        builder.Property(k => k.WorkspaceId).IsRequired();
        builder.Property(k => k.Title).IsRequired();
        builder.Property(k => k.Description).IsRequired();
        builder.Property(k => k.Status).IsRequired();
        builder.Property(k => k.Priority).IsRequired();
        builder.Property(k => k.CreatedById).IsRequired();
        builder.Property(k => k.AssignedToId).IsRequired(false);
        builder.Property(k => k.CategoryId).IsRequired(false);
        builder.Property(k => k.CreatedAt).IsRequired();
        builder.Property(k => k.UpdatedAt).IsRequired();
        builder.Property(k => k.ResolvedAt).IsRequired(false);
        builder.Property(k => k.ClosedAt).IsRequired(false);
    }
}
