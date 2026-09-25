using Kwestie.Application.Kwesties;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Infrastructure.Persistence.Repositories;

public sealed class KwestieRepository(KwestieDbContext context) : IKwestieRepository
{
    public async Task AddAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default)
    {
        context.Kwesties.Add(kwestie);
        await context.SaveChangesAsync(cancellationToken);
    }
}
