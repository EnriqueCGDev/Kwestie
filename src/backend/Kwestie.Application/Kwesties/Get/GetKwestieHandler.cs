using Kwestie.Application.Workspaces;

namespace Kwestie.Application.Kwesties.Get;

public sealed class GetKwestieHandler(IKwestieRepository repository, IWorkspaceRepository workspaceRepository)
{
    private readonly IKwestieRepository _repository = repository;
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;

    public async Task<KwestieDetails?> HandleAsync(
        GetKwestieQuery query, CancellationToken cancellationToken = default)
    {
        if (!await _workspaceRepository.HasActiveMembershipAsync(
            query.WorkspaceId, query.UserId, cancellationToken))
            return null;

        return await _repository.GetForWorkspaceAsync(query.WorkspaceId, query.KwestieId, cancellationToken);
    }
}
