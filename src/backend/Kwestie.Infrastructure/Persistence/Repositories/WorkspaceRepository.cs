using Kwestie.Application.Workspaces;
using Kwestie.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace Kwestie.Infrastructure.Persistence.Repositories;

public sealed class WorkspaceRepository(KwestieDbContext context) : IWorkspaceRepository
{
    private readonly KwestieDbContext _context = context;

    public Task<bool> HasActiveMembershipAsync(
        Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
    {
        // The membership FK guarantees that its workspace exists; no second query is needed.
        return _context.WorkspaceMembers.AnyAsync(
            member => member.WorkspaceId == workspaceId && member.UserId == userId && member.IsActive,
            cancellationToken);
    }

    public async Task AddAsync(
        Workspace workspace,
        WorkspaceMember initialMember,
        CancellationToken cancellationToken = default)
    {
        _context.Workspaces.Add(workspace);
        _context.WorkspaceMembers.Add(initialMember);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId, CancellationToken cancellationToken = default)
    {
        return await (
            from workspace in _context.Workspaces.AsNoTracking()
            join member in _context.WorkspaceMembers.AsNoTracking()
                on workspace.Id equals member.WorkspaceId
            where member.UserId == userId && member.IsActive
            orderby workspace.CreatedAt, workspace.Id
            select workspace).ToListAsync(cancellationToken);
    }
}
