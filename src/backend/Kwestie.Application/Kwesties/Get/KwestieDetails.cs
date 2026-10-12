using Kwestie.Domain.Kwesties;

namespace Kwestie.Application.Kwesties.Get;

public sealed record KwestieDetails(
    Guid KwestieId,
    string Title,
    string Description,
    KwestieStatus Status,
    KwestiePriority Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt);
