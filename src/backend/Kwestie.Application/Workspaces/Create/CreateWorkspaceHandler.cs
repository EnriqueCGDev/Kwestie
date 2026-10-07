using Kwestie.Domain.Workspaces;

namespace Kwestie.Application.Workspaces.Create;

public sealed class CreateWorkspaceHandler(
    IWorkspaceRepository repository,
    TimeProvider timeProvider)
{
    private readonly IWorkspaceRepository _repository = repository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CreateWorkspaceResult> HandleAsync(
        CreateWorkspaceCommand command,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var createdAt = _timeProvider.GetUtcNow();
        var workspace = new Workspace(id, command.Name, createdAt);
        var member = new WorkspaceMember(id, command.UserId, WorkspaceRole.Admin, createdAt);

        await _repository.AddAsync(workspace, member, cancellationToken);

        return new CreateWorkspaceResult(workspace.Id);
    }
}
