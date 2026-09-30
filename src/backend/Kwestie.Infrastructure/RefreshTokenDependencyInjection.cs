using Kwestie.Application.Authentication.Tokens;
using Kwestie.Infrastructure.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kwestie.Infrastructure;

public static class RefreshTokenDependencyInjection
{
    public static IServiceCollection AddRefreshTokens(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RefreshTokenOptions>()
            .Bind(configuration.GetSection(RefreshTokenOptions.SectionName))
            .Validate(options => options.LifetimeDays > 0, "RefreshTokens:LifetimeDays must be greater than zero.")
            .ValidateOnStart();
        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddScoped<IRefreshTokenService, RefreshTokenService>();
        return services;
    }
}
