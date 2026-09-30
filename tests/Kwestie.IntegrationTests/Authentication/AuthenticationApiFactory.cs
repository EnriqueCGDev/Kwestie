using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Kwestie.IntegrationTests.Authentication;

public sealed class AuthenticationApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _configuration = ReadConfiguration();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        // Program reads the connection string before Build, so supply it during host bootstrap.
        builder.ConfigureHostConfiguration(configuration => configuration.AddInMemoryCollection(_configuration));
        return base.CreateHost(builder);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(_configuration));
    }

    private static Dictionary<string, string?> ReadConfiguration()
    {
        using var secrets = new ConfigurationManager();
        secrets.AddUserSecrets<AuthenticationApiFactory>(optional: true);
        var connectionString = secrets.GetConnectionString("Kwestie");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "ConnectionStrings:Kwestie is required in shared API User Secrets; the local database must already be migrated.");

        var values = JwtTestConfiguration.Values();
        values["ConnectionStrings:Kwestie"] = connectionString;
        values["RefreshTokens:LifetimeDays"] = "30";
        return values;
    }

    public HttpClient CreateHttpsClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        AllowAutoRedirect = false,
        // Send each cookie explicitly so replaying A cannot overwrite the current B cookie.
        HandleCookies = false
    });
}
