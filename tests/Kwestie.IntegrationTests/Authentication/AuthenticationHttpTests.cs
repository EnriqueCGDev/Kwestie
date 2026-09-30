using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kwestie.Api.Contracts.Authentication;
using Kwestie.Infrastructure.Persistence;
using Kwestie.Infrastructure.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace Kwestie.IntegrationTests.Authentication;

public sealed class AuthenticationHttpTests(AuthenticationApiFactory factory)
    : IClassFixture<AuthenticationApiFactory>
{
    private const string CookieName = "kwestie_refresh_token";
    private const string Password = "HTTP-Test-Password1!";
    private readonly AuthenticationApiFactory _factory = factory;

    [Fact]
    public async Task Register_PersistsUser_RejectsDuplicate_WithoutIssuingTokens()
    {
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        try
        {
            var userId = await RegisterAsync(client, email);
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
                Assert.False(context.Database.HasPendingModelChanges());
                var user = await context.Users.AsNoTracking().SingleAsync(user => user.Id == userId);
                Assert.Equal(email, user.Email);
                Assert.False(await context.RefreshTokens.AnyAsync(token => token.UserId == userId));
            }

            using var duplicate = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password));
            Assert.Equal(HttpStatusCode.BadRequest, duplicate.StatusCode);
            var errors = await duplicate.Content.ReadFromJsonAsync<RegistrationErrorsResponse>();
            Assert.NotNull(errors);
            Assert.NotEmpty(errors.Errors);
            Assert.False(duplicate.Headers.Contains(HeaderNames.SetCookie));
        }
        finally
        {
            await CleanupAsync(email);
        }
    }

    [Fact]
    public async Task Login_WrongPasswordAndUnknownEmail_ReturnEquivalentUnauthorizedWithoutCookie()
    {
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        try
        {
            var userId = await RegisterAsync(client, email);
            using var wrong = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Wrong-Password1!"));
            using var missing = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(UniqueEmail(), Password));
            await AssertUnauthorizedAsync(wrong, deleteCookie: false);
            await AssertUnauthorizedAsync(missing, deleteCookie: false);
            using var wrongBody = JsonDocument.Parse(await wrong.Content.ReadAsStringAsync());
            using var missingBody = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
            // ProblemDetails trace IDs identify requests; the public failure is otherwise equivalent.
            foreach (var field in new[] { "type", "title", "status" })
                Assert.Equal(wrongBody.RootElement.GetProperty(field).ToString(),
                    missingBody.RootElement.GetProperty(field).ToString());
            await using var scope = _factory.Services.CreateAsyncScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<KwestieDbContext>()
                .RefreshTokens.AnyAsync(token => token.UserId == userId));
        }
        finally
        {
            await CleanupAsync(email);
        }
    }

    [Fact]
    public async Task Login_ReturnsValidJwtAndSecureCookie_WithoutRawRefreshInJson()
    {
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        try
        {
            var userId = await RegisterAsync(client, email);
            using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
            await AssertSessionAsync(response, userId);
        }
        finally
        {
            await CleanupAsync(email);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("malformed-token")]
    public async Task Refresh_MissingOrInvalidCookie_ReturnsUnauthorizedAndDeletesCookie(string? token)
    {
        using var client = _factory.CreateHttpsClient();
        using var response = await RefreshAsync(client, token);
        await AssertUnauthorizedAsync(response, deleteCookie: true);
    }

    [Fact]
    public async Task Refresh_RotatesAtoBtoC_RejectsReuse_WithoutInvalidatingReplacement()
    {
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        try
        {
            var userId = await RegisterAsync(client, email);
            using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
            var (sessionA, cookieA) = await AssertSessionAsync(login, userId);

            using var firstRefresh = await RefreshAsync(client, cookieA);
            var (sessionB, cookieB) = await AssertSessionAsync(firstRefresh, userId);
            Assert.True(cookieA != cookieB);
            Assert.True(sessionA.AccessToken != sessionB.AccessToken);

            using var replayClient = _factory.CreateHttpsClient();
            using var replayA = await RefreshAsync(replayClient, cookieA);
            await AssertUnauthorizedAsync(replayA, deleteCookie: true);
            using var replayAAgain = await RefreshAsync(replayClient, cookieA);
            await AssertUnauthorizedAsync(replayAAgain, deleteCookie: true);

            using var secondRefresh = await RefreshAsync(client, cookieB);
            var (sessionC, cookieC) = await AssertSessionAsync(secondRefresh, userId);
            Assert.True(cookieB != cookieC && cookieA != cookieC);
            Assert.True(sessionB.AccessToken != sessionC.AccessToken);

            using var replayB = await RefreshAsync(replayClient, cookieB);
            await AssertUnauthorizedAsync(replayB, deleteCookie: true);
        }
        finally
        {
            await CleanupAsync(email);
        }
    }

    [Fact]
    public async Task Logout_WithExpiredAccessToken_RevokesRefresh_AndIsIdempotent()
    {
        // Automatic cookie handling proves the browser path covers both Refresh and Logout.
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), HandleCookies = true, AllowAutoRedirect = false
        });
        var email = UniqueEmail();
        try
        {
            var userId = await RegisterAsync(client, email);
            using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
            await AssertSessionAsync(login, userId);
            using var refreshed = await client.PostAsync("/api/auth/refresh", null);
            var (_, token) = await AssertSessionAsync(refreshed, userId);

            var expired = new JwtAccessTokenGenerator(_factory.Services.GetRequiredService<IOptions<JwtOptions>>(),
                new PastTimeProvider()).Generate(userId);
            Assert.True(expired.ExpiresAtUtc < DateTimeOffset.UtcNow);
            client.DefaultRequestHeaders.Authorization = new("Bearer", expired.AccessToken);
            using var logout = await client.PostAsync("/api/auth/logout", null);
            await AssertLoggedOutAsync(logout);

            DateTimeOffset? revokedAt;
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
                var tokens = await context.RefreshTokens.AsNoTracking().Where(row => row.UserId == userId).ToListAsync();
                Assert.Equal(2, tokens.Count);
                Assert.All(tokens, row => Assert.NotNull(row.RevokedAtUtc));
                revokedAt = tokens.Max(row => row.RevokedAtUtc);
            }

            using var replay = _factory.CreateHttpsClient();
            using var refreshRejected = await RefreshAsync(replay, token);
            await AssertUnauthorizedAsync(refreshRejected, deleteCookie: true);
            using var repeated = await LogoutAsync(replay, token);
            await AssertLoggedOutAsync(repeated);
            await using var readScope = _factory.Services.CreateAsyncScope();
            Assert.Equal(revokedAt, await readScope.ServiceProvider.GetRequiredService<KwestieDbContext>()
                .RefreshTokens.Where(row => row.UserId == userId).MaxAsync(row => row.RevokedAtUtc));
        }
        finally { await CleanupAsync(email); }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("malformed-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Logout_MissingMalformedOrUnknownToken_ReturnsSameNoContent(string? token)
    {
        using var client = _factory.CreateHttpsClient();
        using var response = await LogoutAsync(client, token);
        await AssertLoggedOutAsync(response);
    }

    [Fact]
    public async Task Logout_ExpiredRefresh_ReturnsNoContent_WithoutChangingOtherSession()
    {
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        try
        {
            var userId = await RegisterAsync(client, email);
            using var first = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
            var (_, expired) = await AssertSessionAsync(first, userId);
            await using (var scope = _factory.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<KwestieDbContext>().RefreshTokens
                    .Where(row => row.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.ExpiresAtUtc, DateTimeOffset.UtcNow.AddDays(-1)));
            using var second = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
            var (_, active) = await AssertSessionAsync(second, userId);
            using var logout = await LogoutAsync(client, expired);
            await AssertLoggedOutAsync(logout);
            using var renewed = await RefreshAsync(client, active);
            await AssertSessionAsync(renewed, userId);
        }
        finally { await CleanupAsync(email); }
    }

    private static async Task<HttpResponseMessage> LogoutAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        if (token is not null)
            request.Headers.Add(HeaderNames.Cookie, $"{CookieName}={token}");
        return await client.SendAsync(request);
    }

    private static async Task AssertLoggedOutAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        var cookie = ReadCookie(response);
        AssertCookieOptions(cookie);
        Assert.True(string.IsNullOrEmpty(cookie.Value.Value));
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
    }

    private sealed class PastTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UtcNow.AddDays(-1);
    }

    private static string UniqueEmail() => $"HttpAuth-{Guid.NewGuid():N}@example.com";

    private static async Task<Guid> RegisterAsync(HttpClient client, string email)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(["userId"], document.RootElement.EnumerateObject().Select(property => property.Name).ToArray());
        var result = JsonSerializer.Deserialize<RegisterResponse>(json, JsonSerializerOptions.Web);
        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.UserId);
        return result.UserId;
    }

    private async Task<(AuthenticationResponse Session, string Cookie)> AssertSessionAsync(
        HttpResponseMessage response, Guid userId)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(json);
        Assert.Equal(new[] { "accessToken", "accessTokenExpiresAtUtc", "refreshTokenExpiresAtUtc", "userId" },
            document.RootElement.EnumerateObject().Select(property => property.Name).Order().ToArray());
        var session = JsonSerializer.Deserialize<AuthenticationResponse>(json, JsonSerializerOptions.Web);
        Assert.NotNull(session);
        Assert.Equal(userId, session.UserId);

        var cookie = ReadCookie(response);
        AssertCookieOptions(cookie);
        Assert.False(string.IsNullOrWhiteSpace(cookie.Value.Value));
        Assert.True(!json.Contains(cookie.Value.Value!, StringComparison.Ordinal));
        Assert.Equal(session.RefreshTokenExpiresAtUtc.ToUnixTimeSeconds(), cookie.Expires!.Value.ToUnixTimeSeconds());
        Assert.True(session.RefreshTokenExpiresAtUtc > session.AccessTokenExpiresAtUtc);

        var bearer = _factory.Services.GetRequiredService<IOptionsMonitor<JwtBearerOptions>>()
            .Get(JwtBearerDefaults.AuthenticationScheme);
        var validated = await bearer.TokenHandlers.Single().ValidateTokenAsync(
            session.AccessToken, bearer.TokenValidationParameters);
        Assert.True(validated.IsValid);
        Assert.Equal(userId.ToString(), validated.ClaimsIdentity.FindFirst("sub")?.Value);
        Assert.Equal(session.AccessTokenExpiresAtUtc.UtcDateTime, validated.SecurityToken.ValidTo);
        return (session, cookie.Value.Value!);
    }

    private static async Task<HttpResponseMessage> RefreshAsync(HttpClient client, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        if (token is not null)
            request.Headers.Add(HeaderNames.Cookie, $"{CookieName}={token}");
        return await client.SendAsync(request);
    }

    private static async Task AssertUnauthorizedAsync(HttpResponseMessage response, bool deleteCookie)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.False(document.RootElement.TryGetProperty("accessToken", out _));
        Assert.False(document.RootElement.TryGetProperty("refreshToken", out _));
        Assert.False(document.RootElement.TryGetProperty("detail", out _));
        if (!deleteCookie)
        {
            Assert.False(response.Headers.Contains(HeaderNames.SetCookie));
            return;
        }

        var cookie = ReadCookie(response);
        AssertCookieOptions(cookie);
        Assert.True(string.IsNullOrEmpty(cookie.Value.Value));
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);
    }

    private static SetCookieHeaderValue ReadCookie(HttpResponseMessage response) =>
        Assert.Single(SetCookieHeaderValue.ParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList()),
            cookie => cookie.Name == CookieName);

    private static void AssertCookieOptions(SetCookieHeaderValue cookie)
    {
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/api/auth", cookie.Path.Value);
        Assert.False(cookie.Domain.HasValue);
    }

    private async Task CleanupAsync(string email)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
        var userIds = await context.Users.Where(user => user.Email == email).Select(user => user.Id).ToArrayAsync();
        await context.Users.Where(user => user.Email == email).ExecuteDeleteAsync();
        Assert.False(await context.Users.AnyAsync(user => user.Email == email));
        Assert.False(await context.RefreshTokens.AnyAsync(token => userIds.Contains(token.UserId)));
    }
}
