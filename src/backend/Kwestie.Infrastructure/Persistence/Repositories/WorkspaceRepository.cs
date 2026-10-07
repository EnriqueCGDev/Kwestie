using Kwestie.Application.Workspaces;
using Kwestie.Domain.Workspaces;

namespace Kwestie.Infrastructure.Persistence.Repositories;

public sealed class WorkspaceRepository(KwestieDbContext context) : IWorkspaceRepository
{
    private readonly KwestieDbContext _context = context;

    public async Task AddAsync(
        Workspace workspace,
        WorkspaceMember initialMember,
        CancellationToken cancellationToken = default)
    {
        _context.Workspaces.Add(workspace);
        _context.WorkspaceMembers.Add(initialMember);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
