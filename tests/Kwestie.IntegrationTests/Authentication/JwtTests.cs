using System.IdentityModel.Tokens.Jwt;
using Kwestie.Application.Authentication.Tokens;
using Kwestie.Infrastructure;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Kwestie.IntegrationTests.Authentication;

public class JwtTests
{
    [Fact]
    public async Task Generate_UsesConfiguredClaimsSignatureAndLifetime_AndBearerPreservesSubject()
    {
        var now = DateTimeOffset.UtcNow;
        var clock = new FixedTimeProvider(now);
        using var provider = CreateProvider(clock);
        Assert.Same(clock, provider.GetRequiredService<TimeProvider>());
        var generator = provider.GetRequiredService<IAccessTokenGenerator>();
        var userId = Guid.NewGuid();
        var generated = generator.Generate(userId);
        Assert.False(string.IsNullOrWhiteSpace(generated.AccessToken));

        var bearer = GetBearer(provider);
        var validation = await bearer.TokenHandlers.Single().ValidateTokenAsync(
            generated.AccessToken, bearer.TokenValidationParameters);
        Assert.True(validation.IsValid);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(generated.AccessToken);
        Assert.Equal("HS256", token.Header.Alg);
        Assert.Equal("Kwestie.Tests", token.Issuer);
        Assert.Equal("Kwestie.Tests.Api", Assert.Single(token.Audiences));
        Assert.Equal(userId.ToString(), token.Subject);
        Assert.True(Guid.TryParse(token.Id, out _));
        var issuedAt = DateTimeOffset.FromUnixTimeSeconds(now.ToUnixTimeSeconds());
        Assert.Equal(issuedAt.UtcDateTime, token.IssuedAt);
        Assert.Equal(issuedAt.UtcDateTime, token.ValidFrom);
        Assert.Equal(issuedAt.AddMinutes(15), generated.ExpiresAtUtc);
        Assert.Equal(generated.ExpiresAtUtc.UtcDateTime, token.ValidTo);
        Assert.Equal(TimeSpan.Zero, generated.ExpiresAtUtc.Offset);
        Assert.Equal(new[] { "aud", "exp", "iat", "iss", "jti", "nbf", "sub" },
            token.Payload.Keys.OrderBy(key => key).ToArray());
        var second = new JwtSecurityTokenHandler().ReadJwtToken(generator.Generate(userId).AccessToken);
        Assert.NotEqual(token.Id, second.Id);

        // Exercise the registered Bearer handler without adding an HTTP endpoint.
        using var scope = provider.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Headers.Authorization = "Bearer " + generated.AccessToken;
        var authenticated = await context.AuthenticateAsync();
        Assert.True(authenticated.Succeeded);
        Assert.Equal(userId.ToString(), authenticated.Principal!.FindFirst("sub")?.Value);
        Assert.Equal(userId.ToString(), authenticated.Principal.Identity!.Name);
    }

    [Theory]
    [InlineData("signature")]
    [InlineData("key")]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("expired")]
    [InlineData("future")]
    public async Task BearerValidation_RejectsInvalidTokens(string scenario)
    {
        using var validator = CreateProvider();
        var values = JwtTestConfiguration.Values();
        var now = DateTimeOffset.UtcNow;
        switch (scenario)
        {
            case "key": values["Jwt:Key"] = "OTHER-TEST-ONLY-KEY-0123456789abcdef"; break;
            case "issuer": values["Jwt:Issuer"] = "Other.Issuer"; break;
            case "audience": values["Jwt:Audience"] = "Other.Audience"; break;
            case "expired": now = now.AddMinutes(-30); break;
            case "future": now = now.AddMinutes(30); break;
        }
        using var issuer = CreateProvider(new FixedTimeProvider(now), values);
        var token = issuer.GetRequiredService<IAccessTokenGenerator>().Generate(Guid.NewGuid()).AccessToken;
        if (scenario == "signature")
        {
            var parts = token.Split('.');
            parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];
            token = string.Join('.', parts);
        }

        var bearer = GetBearer(validator);
        var validation = await bearer.TokenHandlers.Single().ValidateTokenAsync(token, bearer.TokenValidationParameters);
        Assert.False(validation.IsValid);
    }

    [Theory]
    [InlineData("Jwt:Issuer", "")]
    [InlineData("Jwt:Audience", " ")]
    [InlineData("Jwt:AccessTokenMinutes", "0")]
    [InlineData("Jwt:AccessTokenMinutes", "-1")]
    [InlineData("Jwt:Key", null)]
    [InlineData("Jwt:Key", "                               ")]
    [InlineData("Jwt:Key", "1234567890123456789012345678901")]
    public async Task Startup_InvalidConfiguration_FailsWithoutDisclosingValues(string setting, string? value)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        var values = JwtTestConfiguration.Values();
        values[setting] = value;
        builder.Configuration.AddInMemoryCollection(values);
        builder.Services.AddJwtAuthentication(builder.Configuration);
        using var host = builder.Build();

        var exception = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains(setting, exception.Message);
        Assert.DoesNotContain(values["Jwt:Key"] ?? "UNSET-KEY", exception.Message);
    }

    [Fact]
    public void Generate_EmptyUserId_IsRejected()
    {
        using var provider = CreateProvider();
        Assert.Throws<ArgumentException>(() => provider.GetRequiredService<IAccessTokenGenerator>().Generate(Guid.Empty));
    }

    private static ServiceProvider CreateProvider(TimeProvider? clock = null, Dictionary<string, string?>? values = null)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        if (clock is not null)
            services.AddSingleton(clock);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values ?? JwtTestConfiguration.Values()).Build();
        services.AddJwtAuthentication(configuration);
        return services.BuildServiceProvider();
    }

    private static JwtBearerOptions GetBearer(IServiceProvider provider) =>
        provider.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>().Get(JwtBearerDefaults.AuthenticationScheme);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        private readonly DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
