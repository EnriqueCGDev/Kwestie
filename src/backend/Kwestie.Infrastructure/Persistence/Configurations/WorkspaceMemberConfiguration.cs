using Kwestie.Domain.Workspaces;
using Kwestie.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kwestie.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceMemberConfiguration : IEntityTypeConfiguration<WorkspaceMember>
{
    public void Configure(EntityTypeBuilder<WorkspaceMember> builder)
    {
        builder.ToTable("WorkspaceMembers");
        builder.HasKey(member => new { member.WorkspaceId, member.UserId });
        builder.Property(member => member.WorkspaceId).ValueGeneratedNever().IsRequired();
        builder.Property(member => member.UserId).ValueGeneratedNever().IsRequired();
        builder.Property(member => member.Role).IsRequired();
        builder.Property(member => member.JoinedAt).IsRequired();
        builder.Property(member => member.IsActive).IsRequired();

        builder.HasOne<Workspace>().WithMany()
            .HasForeignKey(member => member.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(member => member.UserId).OnDelete(DeleteBehavior.NoAction);
    }
}
