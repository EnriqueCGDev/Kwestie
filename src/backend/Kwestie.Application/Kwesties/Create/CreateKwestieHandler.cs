using Kwestie.Application.Workspaces;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Kwesties.Create;

public sealed class CreateKwestieHandler(
    IKwestieRepository repository,
    IWorkspaceRepository workspaceRepository,
    TimeProvider timeProvider)
{
    private readonly IKwestieRepository _repository = repository;
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<CreateKwestieResult> HandleAsync(
        CreateKwestieCommand command,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var createdAt = _timeProvider.GetUtcNow();
        var kwestie = new KwestieEntity(
            id, command.WorkspaceId, command.Title, command.Description,
            command.Priority, command.CreatedById, createdAt, command.CategoryId);

        if (!await _workspaceRepository.HasActiveMembershipAsync(
            command.WorkspaceId, command.CreatedById, cancellationToken))
            throw new CreateKwestieAccessDeniedException();

        await _repository.AddAsync(kwestie, cancellationToken);

        return new CreateKwestieResult(kwestie.Id);
    }
}
