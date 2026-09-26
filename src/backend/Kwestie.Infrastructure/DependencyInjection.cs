using Kwestie.Application.Kwesties;
using Kwestie.Application.Authentication.Register;
using Kwestie.Infrastructure.Identity;
using Kwestie.Infrastructure.Persistence;
using Kwestie.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kwestie.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        services.AddDbContext<KwestieDbContext>(options => options.UseSqlServer(connectionString));
        services.AddScoped<IKwestieRepository, KwestieRepository>();
        services.AddIdentityCore<ApplicationUser>(options => options.User.RequireUniqueEmail = true)
            .AddEntityFrameworkStores<KwestieDbContext>();
        services.AddScoped<IUserRegistration, UserRegistration>();
        return services;
    }
}
