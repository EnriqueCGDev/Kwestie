using Kwestie.Application.Workspaces.List;

namespace Kwestie.Application.Workspaces.Get;

public sealed class GetWorkspaceHandler(IWorkspaceRepository repository)
{
    private readonly IWorkspaceRepository _repository = repository;

    public async Task<WorkspaceSummary?> HandleAsync(
        GetWorkspaceQuery query, CancellationToken cancellationToken = default)
    {
        var workspace = await _repository.GetForUserAsync(query.WorkspaceId, query.UserId, cancellationToken);
        return workspace is null ? null : new WorkspaceSummary(workspace.Id, workspace.Name, workspace.CreatedAt);
    }
}
