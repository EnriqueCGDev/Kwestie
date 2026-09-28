using Kwestie.Application.Kwesties;
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
}
