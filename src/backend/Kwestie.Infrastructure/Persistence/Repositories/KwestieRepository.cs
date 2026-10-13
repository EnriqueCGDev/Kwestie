using Kwestie.Application.Kwesties;
using Kwestie.Application.Kwesties.Get;
using Kwestie.Application.Kwesties.List;
using Microsoft.EntityFrameworkCore;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Infrastructure.Persistence.Repositories;

public sealed class KwestieRepository(KwestieDbContext context) : IKwestieRepository
{
    private readonly KwestieDbContext _context = context;

    public Task<KwestieEntity?> GetForUpdateAsync(
        Guid workspaceId, Guid kwestieId, CancellationToken cancellationToken = default) =>
        _context.Kwesties.SingleOrDefaultAsync(
            kwestie => kwestie.WorkspaceId == workspaceId && kwestie.Id == kwestieId, cancellationToken);

    public async Task<bool> UpdateAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(kwestie).State == EntityState.Detached)
            throw new InvalidOperationException("Load the kwestie through GetForUpdateAsync before updating it.");

        try
        {
            await _context.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, kwestie))
        {
            // Do not let a later save accidentally retry the losing write.
            exception.Entries[0].State = EntityState.Detached;
            return false;
        }
    }

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

    public Task<KwestieDetails?> GetForWorkspaceAsync(
        Guid workspaceId, Guid kwestieId, CancellationToken cancellationToken = default)
    {
        return _context.Kwesties.AsNoTracking()
            .Where(kwestie => kwestie.WorkspaceId == workspaceId && kwestie.Id == kwestieId)
            .Select(kwestie => new KwestieDetails(
                kwestie.Id, kwestie.Title, kwestie.Description, kwestie.Status, kwestie.Priority,
                kwestie.CreatedAt, kwestie.UpdatedAt, kwestie.ResolvedAt, kwestie.ClosedAt))
            .SingleOrDefaultAsync(cancellationToken);
    }
}
