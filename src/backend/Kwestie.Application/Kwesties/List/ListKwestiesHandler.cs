using Kwestie.Application.Workspaces;

namespace Kwestie.Application.Kwesties.List;

public sealed class ListKwestiesHandler(IKwestieRepository repository, IWorkspaceRepository workspaceRepository)
{
    private readonly IKwestieRepository _repository = repository;
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;

    public async Task<IReadOnlyList<KwestieSummary>?> HandleAsync(
        ListKwestiesQuery query, CancellationToken cancellationToken = default)
    {
        if (!await _workspaceRepository.HasActiveMembershipAsync(
            query.WorkspaceId, query.UserId, cancellationToken))
            return null;

        return await _repository.ListForWorkspaceAsync(query.WorkspaceId, cancellationToken);
    }
}
