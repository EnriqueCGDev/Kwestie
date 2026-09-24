using Kwestie.Domain.Kwesties;

namespace Kwestie.Application.Kwesties.Create;

public sealed record CreateKwestieCommand(
    Guid WorkspaceId,
    string Title,
    string? Description,
    KwestiePriority Priority,
    Guid CreatedById,
    Guid? CategoryId);
