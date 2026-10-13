using Kwestie.Application.Kwesties.Get;
using Kwestie.Application.Kwesties.List;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Kwesties;

public interface IKwestieRepository
{
    Task<KwestieEntity?> GetForUpdateAsync(
        Guid workspaceId, Guid kwestieId, CancellationToken cancellationToken = default);

    /// <summary>Saves the tracked entity; false means an optimistic concurrency conflict.</summary>
    Task<bool> UpdateAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default);

    Task AddAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KwestieSummary>> ListForWorkspaceAsync(
        Guid workspaceId, CancellationToken cancellationToken = default);

    Task<KwestieDetails?> GetForWorkspaceAsync(
        Guid workspaceId, Guid kwestieId, CancellationToken cancellationToken = default);
}
