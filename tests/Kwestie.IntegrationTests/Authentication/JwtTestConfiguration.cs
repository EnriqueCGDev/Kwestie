using Microsoft.Extensions.Configuration;

namespace Kwestie.IntegrationTests.Authentication;

internal static class JwtTestConfiguration
{
    // Public test material only. Never use these values outside automated tests.
    public static Dictionary<string, string?> Values() => new()
    {
        ["Jwt:Issuer"] = "Kwestie.Tests",
        ["Jwt:Audience"] = "Kwestie.Tests.Api",
        ["Jwt:AccessTokenMinutes"] = "15",
        ["Jwt:Key"] = "TEST-ONLY-KEY-NOT-A-SECRET-0123456789abcdef"
    };

    public static IConfigurationRoot Create() => new ConfigurationBuilder()
        .AddInMemoryCollection(Values()).Build();
}
