using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Application.Kwesties;

public interface IKwestieRepository
{
    Task AddAsync(KwestieEntity kwestie, CancellationToken cancellationToken = default);
}
