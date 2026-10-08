using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Kwestie.Api.Contracts.Authentication;
using Kwestie.Api.Contracts.Kwesties;
using Kwestie.Api.Contracts.Workspaces;
using Kwestie.Domain.Kwesties;
using Kwestie.Domain.Workspaces;
using Kwestie.Infrastructure.Authentication;
using Kwestie.Infrastructure.Persistence;
using Kwestie.IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kwestie.IntegrationTests.Kwesties;

public sealed class KwestieHttpTests(AuthenticationApiFactory factory) : IClassFixture<AuthenticationApiFactory>
{
    private const string Password = "Kwestie-HTTP-Test1!";
    private readonly AuthenticationApiFactory _factory = factory;

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-jwt")]
    public async Task Create_WithoutValidBearer_ReturnsUnauthorized(string? token)
    {
        using var client = _factory.CreateHttpsClient();
        if (token is not null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await client.PostAsJsonAsync(Path(Guid.NewGuid()), ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Create_WithSignedTokenButInvalidSub_ReturnsUnauthorized(string? subject)
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SignedToken(subject));

        using var response = await client.PostAsJsonAsync(Path(Guid.NewGuid()), ValidRequest());

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(WorkspaceRole.Admin, KwestiePriority.Normal)]
    [InlineData(WorkspaceRole.Member, KwestiePriority.Critical)]
    public async Task Create_ActiveMember_PersistsForRouteAndJwtSubject_WithGeneratedNumber(
        WorkspaceRole role, KwestiePriority priority)
    {
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        var workspaceIds = new List<Guid>();
        try
        {
            var userId = await AuthenticateAsync(client, email);
            var workspaceId = await CreateWorkspaceAsync(client, workspaceIds);
            if (role == WorkspaceRole.Member)
            {
                await using var fixtureScope = _factory.Services.CreateAsyncScope();
                // Fixture only; this does not implement membership management.
                await fixtureScope.ServiceProvider.GetRequiredService<KwestieDbContext>().WorkspaceMembers
                    .Where(m => m.WorkspaceId == workspaceId && m.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.Role, WorkspaceRole.Member));
            }

            var spoofedUserId = Guid.NewGuid();
            var spoofedWorkspaceId = Guid.NewGuid();
            client.DefaultRequestHeaders.Add("CreatedById", spoofedUserId.ToString());
            client.DefaultRequestHeaders.Add("UserId", spoofedUserId.ToString());
            client.DefaultRequestHeaders.Add("WorkspaceId", spoofedWorkspaceId.ToString());
            var payload = new Dictionary<string, object?> { ["title"] = "  Broken printer  ", ["priority"] = (int)priority };
            if (role == WorkspaceRole.Admin)
                payload["description"] = "  Cannot print.  ";
            using var response = await client.PostAsJsonAsync(
                $"{Path(workspaceId)}?createdById={spoofedUserId}&userId={spoofedUserId}&workspaceId={spoofedWorkspaceId}", payload);

            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            Assert.Null(response.Headers.Location);
            var result = await response.Content.ReadFromJsonAsync<CreateKwestieResponse>();
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.KwestieId);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(["kwestieId"], json.RootElement.EnumerateObject().Select(p => p.Name).ToArray());

            // A separate request scope/context verifies committed data, not tracked state.
            await using var readScope = _factory.Services.CreateAsyncScope();
            var context = readScope.ServiceProvider.GetRequiredService<KwestieDbContext>();
            Assert.False(context.Database.HasPendingModelChanges());
            var stored = await context.Kwesties.AsNoTracking().SingleAsync(k => k.Id == result.KwestieId);
            Assert.Equal(workspaceId, stored.WorkspaceId);
            Assert.Equal(userId, stored.CreatedById);
            Assert.Equal("Broken printer", stored.Title);
            Assert.Equal(role == WorkspaceRole.Admin ? "Cannot print." : string.Empty, stored.Description);
            Assert.Equal(priority, stored.Priority);
            Assert.Null(stored.CategoryId);
            Assert.Equal(KwestieStatus.Open, stored.Status);
            Assert.True(stored.Number > 0);
            Assert.Equal(1, await context.Kwesties.CountAsync(k => k.WorkspaceId == workspaceId));
        }
        finally
        {
            await CleanupAsync([email], workspaceIds);
        }
    }

    [Fact]
    public async Task Create_AccessDeniedCases_ReturnIdenticalForbiddenResponsesWithoutInserts()
    {
        using var client = _factory.CreateHttpsClient();
        using var otherClient = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        var otherEmail = UniqueEmail();
        var workspaceIds = new List<Guid>();
        try
        {
            var userId = await AuthenticateAsync(client, email);
            await AuthenticateAsync(otherClient, otherEmail);
            var inactiveId = await CreateWorkspaceAsync(client, workspaceIds);
            var otherWorkspaceId = await CreateWorkspaceAsync(otherClient, workspaceIds);
            var missingId = Guid.NewGuid();
            workspaceIds.Add(missingId);
            await using var scope = _factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
            await context.WorkspaceMembers.Where(m => m.WorkspaceId == inactiveId && m.UserId == userId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.IsActive, false));
            Assert.False(await context.Workspaces.AnyAsync(w => w.Id == missingId));
            var responses = new List<(HttpStatusCode Status, string? ContentType, string Body)>();
            foreach (var workspaceId in new[] { otherWorkspaceId, inactiveId, missingId })
            {
                using var response = await client.PostAsJsonAsync(Path(workspaceId), ValidRequest());
                responses.Add((response.StatusCode, response.Content.Headers.ContentType?.ToString(),
                    await response.Content.ReadAsStringAsync()));
                Assert.False(await context.Kwesties.AnyAsync(k => k.WorkspaceId == workspaceId));
            }
            Assert.All(responses, response =>
            {
                Assert.Equal(HttpStatusCode.Forbidden, response.Status);
                Assert.Equal(string.Empty, response.Body);
                Assert.Equal(responses[0], response);
            });
        }
        finally
        {
            await CleanupAsync([email, otherEmail], workspaceIds);
        }
    }

    [Fact]
    public async Task Create_InvalidContracts_ReturnBadRequestWithoutInserts()
    {
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        var workspaceIds = new List<Guid>();
        try
        {
            var userId = await AuthenticateAsync(client, email);
            var workspaceId = await CreateWorkspaceAsync(client, workspaceIds);
            await using var scope = _factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
            var invalidBodies = new[]
            {
                "{\"title\":null,\"priority\":2}",
                "{\"title\":\"\",\"priority\":2}",
                "{\"title\":\"   \",\"priority\":2}",
                "{\"priority\":2}",
                "{\"title\":\"Printer\",\"priority\":0}",
                "{\"title\":\"Printer\",\"priority\":5}",
                "{\"title\":\"Printer\",\"priority\":-1}",
                "{\"title\":\"Printer\",\"priority\":null}",
                "{\"title\":\"Printer\"}",
                "{\"title\":123,\"priority\":2}",
                "{\"title\":\"Printer\",\"description\":true,\"priority\":2}",
                "{\"title\":\"Printer\",\"priority\":\"Normal\"}",
                "{\"title\":\"Printer\",\"priority\":2.5}",
                "{"
            };
            foreach (var body in invalidBodies)
            {
                using var content = new StringContent(body, Encoding.UTF8, "application/json");
                using var response = await client.PostAsync(Path(workspaceId), content);
                Assert.Equal((body, HttpStatusCode.BadRequest), (body, response.StatusCode));
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
                Assert.False(await context.Kwesties.AnyAsync(k => k.WorkspaceId == workspaceId));
            }
            foreach (var field in new[] { "createdById", "userId", "workspaceId", "categoryId", "id", "number", "status", "unexpected" })
            {
                var body = new Dictionary<string, object?>
                {
                    ["title"] = "Printer", ["priority"] = 2,
                    [field] = field is "number" or "status" ? (object)1 : Guid.NewGuid().ToString()
                };
                using var response = await client.PostAsJsonAsync(Path(workspaceId), body);
                Assert.Equal((field, HttpStatusCode.BadRequest), (field, response.StatusCode));
                Assert.False(await context.Kwesties.AnyAsync(k => k.WorkspaceId == workspaceId));
            }
            // Empty WorkspaceId is still an intrinsic Domain rejection, not access denial.
            using var emptyIdResponse = await client.PostAsJsonAsync(Path(Guid.Empty), ValidRequest());
            Assert.Equal(HttpStatusCode.BadRequest, emptyIdResponse.StatusCode);
            Assert.False(await context.Kwesties.AnyAsync(k => k.CreatedById == userId));
        }
        finally
        {
            await CleanupAsync([email], workspaceIds);
        }
    }

    private async Task<Guid> AuthenticateAsync(HttpClient client, string email)
    {
        using var registration = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest(email, Password));
        Assert.Equal(HttpStatusCode.Created, registration.StatusCode);
        var user = await registration.Content.ReadFromJsonAsync<RegisterResponse>();
        Assert.NotNull(user);
        using var login = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var session = await login.Content.ReadFromJsonAsync<AuthenticationResponse>();
        Assert.NotNull(session);
        Assert.Equal(user.UserId, session.UserId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        return user.UserId;
    }

    private static async Task<Guid> CreateWorkspaceAsync(HttpClient client, List<Guid> workspaceIds)
    {
        using var response = await client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest("Kwestie HTTP tests"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<CreateWorkspaceResponse>();
        Assert.NotNull(result);
        workspaceIds.Add(result.WorkspaceId);
        return result.WorkspaceId;
    }

    private string SignedToken(string? subject)
    {
        var options = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = new List<Claim>();
        if (subject is not null)
            claims.Add(new Claim("sub", subject));
        return new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims), Issuer = options.Issuer, Audience = options.Audience,
            IssuedAt = DateTime.UtcNow, NotBefore = DateTime.UtcNow, Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256)
        });
    }

    private async Task CleanupAsync(string[] emails, List<Guid> workspaceIds)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
        var userIds = await context.Users.Where(u => emails.Contains(u.Email!)).Select(u => u.Id).ToArrayAsync();
        var membershipWorkspaceIds = await context.WorkspaceMembers.Where(m => userIds.Contains(m.UserId))
            .Select(m => m.WorkspaceId).ToArrayAsync();
        var ownedWorkspaceIds = workspaceIds.Concat(membershipWorkspaceIds).Distinct().ToArray();
        // Kwesties have no workspace FK yet, so remove test rows explicitly before their workspaces.
        await context.Kwesties.Where(k => userIds.Contains(k.CreatedById) || ownedWorkspaceIds.Contains(k.WorkspaceId)).ExecuteDeleteAsync();
        await context.Workspaces.Where(w => ownedWorkspaceIds.Contains(w.Id)).ExecuteDeleteAsync();
        await context.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync();
        Assert.False(await context.Kwesties.AnyAsync(k => userIds.Contains(k.CreatedById) || ownedWorkspaceIds.Contains(k.WorkspaceId)));
        Assert.False(await context.Workspaces.AnyAsync(w => ownedWorkspaceIds.Contains(w.Id)));
        Assert.False(await context.WorkspaceMembers.AnyAsync(m => userIds.Contains(m.UserId)));
        Assert.False(await context.Users.AnyAsync(u => userIds.Contains(u.Id)));
    }

    private static CreateKwestieRequest ValidRequest() => new("Printer", "Cannot print", KwestiePriority.Normal);
    private static string Path(Guid workspaceId) => $"/api/workspaces/{workspaceId}/kwesties";
    private static string UniqueEmail() => $"KwestieHttp-{Guid.NewGuid():N}@example.com";
}
