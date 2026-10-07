namespace Kwestie.Application.Workspaces.Create;

public sealed record CreateWorkspaceCommand(string Name, Guid UserId);
