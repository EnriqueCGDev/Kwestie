using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Kwesties.Create;

public sealed class CreateKwestieHandler
{
    private readonly IKwestieRepository _repository;
    private readonly TimeProvider _timeProvider;

    public CreateKwestieHandler(IKwestieRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public async Task<CreateKwestieResult> HandleAsync(
        CreateKwestieCommand command,
        CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var createdAt = _timeProvider.GetUtcNow();
        var kwestie = new KwestieEntity(
            id, command.WorkspaceId, command.Title, command.Description,
            command.Priority, command.CreatedById, createdAt, command.CategoryId);

        await _repository.AddAsync(kwestie, cancellationToken);

        return new CreateKwestieResult(kwestie.Id);
    }
}
