namespace Kwestie.Api.Contracts.Kwesties;

public sealed record KwestieSummaryResponse(
    Guid KwestieId,
    string Title,
    string Description,
    int Status,
    int Priority,
    DateTimeOffset CreatedAt);
