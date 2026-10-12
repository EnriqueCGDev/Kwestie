namespace Kwestie.Api.Contracts.Kwesties;

public sealed record KwestieDetailsResponse(
    Guid KwestieId,
    string Title,
    string Description,
    int Status,
    int Priority,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ResolvedAt,
    DateTimeOffset? ClosedAt);
