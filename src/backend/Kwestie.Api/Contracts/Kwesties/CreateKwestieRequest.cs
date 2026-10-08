using System.Text.Json.Serialization;
using Kwestie.Domain.Kwesties;

namespace Kwestie.Api.Contracts.Kwesties;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateKwestieRequest(
    string? Title,
    string? Description,
    [property: JsonRequired] KwestiePriority Priority);
