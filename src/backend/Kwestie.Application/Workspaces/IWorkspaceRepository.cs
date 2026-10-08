using Kwestie.Domain.Workspaces;

namespace Kwestie.Application.Workspaces;

public interface IWorkspaceRepository
{
    /// <summary>
    /// Persists the workspace and its initial membership atomically.
    /// Completes only after both have been saved successfully.
    /// </summary>
    Task AddAsync(
        Workspace workspace,
        WorkspaceMember initialMember,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Workspace>> ListForUserAsync(
        Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true only for an active membership in an existing workspace, regardless of role.
    /// </summary>
    Task<bool> HasActiveMembershipAsync(
        Guid workspaceId, Guid userId, CancellationToken cancellationToken = default);
}
