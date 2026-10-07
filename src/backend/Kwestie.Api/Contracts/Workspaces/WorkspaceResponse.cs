namespace Kwestie.Api.Contracts.Workspaces;

public sealed record WorkspaceResponse(Guid WorkspaceId, string Name, DateTimeOffset CreatedAt);
