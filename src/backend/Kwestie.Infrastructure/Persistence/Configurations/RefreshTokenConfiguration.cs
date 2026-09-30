using Kwestie.Infrastructure.Authentication;
using Kwestie.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kwestie.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("RefreshTokens");
        builder.HasKey(token => token.Id);
        builder.Property(token => token.Id).ValueGeneratedNever();
        builder.Property(token => token.UserId).IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(token => token.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.Property(token => token.TokenHash).HasColumnType("char(64)")
            .HasMaxLength(64).IsUnicode(false).IsFixedLength().IsRequired();
        builder.HasIndex(token => token.TokenHash).IsUnique();
        builder.Property(token => token.CreatedAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(token => token.ExpiresAtUtc).HasColumnType("datetimeoffset").IsRequired();
        builder.Property(token => token.RevokedAtUtc).HasColumnType("datetimeoffset").IsRequired(false);
        builder.Property(token => token.RowVersion).IsRowVersion().IsRequired();
    }
}
