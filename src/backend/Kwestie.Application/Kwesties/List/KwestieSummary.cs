using Kwestie.Domain.Kwesties;

namespace Kwestie.Application.Kwesties.List;

public sealed record KwestieSummary(
    Guid KwestieId,
    string Title,
    string Description,
    KwestieStatus Status,
    KwestiePriority Priority,
    DateTimeOffset CreatedAt);
