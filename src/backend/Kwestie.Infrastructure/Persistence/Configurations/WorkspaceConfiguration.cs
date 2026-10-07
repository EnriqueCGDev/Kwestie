using Kwestie.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kwestie.Infrastructure.Persistence.Configurations;

public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("Workspaces");
        builder.HasKey(workspace => workspace.Id);
        builder.Property(workspace => workspace.Id).ValueGeneratedNever();
        builder.Property(workspace => workspace.Name).IsRequired();
        builder.Property(workspace => workspace.CreatedAt).IsRequired();
    }
}
