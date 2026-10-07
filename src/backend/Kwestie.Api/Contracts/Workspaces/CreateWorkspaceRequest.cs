using System.Text.Json.Serialization;

namespace Kwestie.Api.Contracts.Workspaces;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateWorkspaceRequest(string? Name);
