namespace Kwestie.Application.Workspaces.List;

public sealed class ListWorkspacesHandler(IWorkspaceRepository repository)
{
    private readonly IWorkspaceRepository _repository = repository;

    public async Task<IReadOnlyList<WorkspaceSummary>> HandleAsync(
        ListWorkspacesQuery query,
        CancellationToken cancellationToken = default)
    {
        var workspaces = await _repository.ListForUserAsync(query.UserId, cancellationToken);
        return workspaces.Select(workspace => new WorkspaceSummary(
            workspace.Id, workspace.Name, workspace.CreatedAt)).ToArray();
    }
}
