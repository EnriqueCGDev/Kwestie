namespace Kwestie.Application.Kwesties.Transition;

public sealed record TransitionKwestieCommand(
    Guid WorkspaceId, Guid KwestieId, Guid UserId, KwestieTransition Transition);
