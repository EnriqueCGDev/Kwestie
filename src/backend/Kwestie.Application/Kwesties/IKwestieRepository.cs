using Kwestie.Application.Kwesties.List;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Kwesties;

public interface IKwestieRepository
{
    Task AddAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KwestieSummary>> ListForWorkspaceAsync(
        Guid workspaceId, CancellationToken cancellationToken = default);
}
