using Kwestie.Application.Kwesties;
using Kwestie.Application.Kwesties.List;
using Microsoft.EntityFrameworkCore;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Infrastructure.Persistence.Repositories;

public sealed class KwestieRepository(KwestieDbContext context) : IKwestieRepository
{
    private readonly KwestieDbContext _context = context;

    public async Task AddAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default)
    {
        _context.Kwesties.Add(kwestie);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<KwestieSummary>> ListForWorkspaceAsync(
        Guid workspaceId, CancellationToken cancellationToken = default)
    {
        return await _context.Kwesties.AsNoTracking()
            .Where(kwestie => kwestie.WorkspaceId == workspaceId)
            .OrderBy(kwestie => kwestie.CreatedAt).ThenBy(kwestie => kwestie.Id)
            .Select(kwestie => new KwestieSummary(
                kwestie.Id, kwestie.Title, kwestie.Description, kwestie.Status, kwestie.Priority, kwestie.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
