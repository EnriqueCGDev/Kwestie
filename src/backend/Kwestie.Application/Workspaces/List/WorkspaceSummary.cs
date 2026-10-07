namespace Kwestie.Application.Workspaces.List;

public sealed record WorkspaceSummary(Guid WorkspaceId, string Name, DateTimeOffset CreatedAt);
