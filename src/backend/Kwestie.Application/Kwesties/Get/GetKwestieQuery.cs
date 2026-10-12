namespace Kwestie.Application.Kwesties.Get;

public sealed record GetKwestieQuery(Guid WorkspaceId, Guid KwestieId, Guid UserId);
