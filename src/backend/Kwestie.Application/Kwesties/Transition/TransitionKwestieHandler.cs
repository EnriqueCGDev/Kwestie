using Kwestie.Application.Workspaces;
using Kwestie.Domain.Common;
using Kwestie.Domain.Workspaces;

namespace Kwestie.Application.Kwesties.Transition;

public sealed class TransitionKwestieHandler(
    IKwestieRepository repository, IWorkspaceRepository workspaceRepository, TimeProvider timeProvider)
{
    private readonly IKwestieRepository _repository = repository;
    private readonly IWorkspaceRepository _workspaceRepository = workspaceRepository;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<TransitionKwestieResult> HandleAsync(
        TransitionKwestieCommand command, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(command.Transition))
            throw new ArgumentOutOfRangeException(nameof(command), "Unknown kwestie transition.");

        var role = await _workspaceRepository.GetActiveRoleAsync(command.WorkspaceId, command.UserId, cancellationToken);
        if (role is null) return TransitionKwestieResult.NotFound;

        var kwestie = await _repository.GetForUpdateAsync(command.WorkspaceId, command.KwestieId, cancellationToken);
        if (kwestie is null) return TransitionKwestieResult.NotFound;
        if (!CanTransition(role.Value, command.Transition)) return TransitionKwestieResult.Forbidden;

        var occurredAt = _timeProvider.GetUtcNow();
        try
        {
            switch (command.Transition)
            {
                case KwestieTransition.StartProgress: kwestie.StartProgress(occurredAt); break;
                case KwestieTransition.Resolve: kwestie.Resolve(occurredAt); break;
                case KwestieTransition.Close: kwestie.Close(occurredAt); break;
            }
        }
        catch (DomainException)
        {
            return TransitionKwestieResult.Conflict;
        }

        return await _repository.UpdateAsync(kwestie, cancellationToken)
            ? TransitionKwestieResult.Applied : TransitionKwestieResult.Conflict;
    }

    private static bool CanTransition(WorkspaceRole role, KwestieTransition transition) =>
        role == WorkspaceRole.Admin || (role == WorkspaceRole.Member && transition != KwestieTransition.Close);
}
