using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Kwestie.Api.Contracts.Authentication;
using Kwestie.Api.Contracts.Workspaces;
using Kwestie.Application.Workspaces;
using Kwestie.Domain.Workspaces;
using Kwestie.Infrastructure.Authentication;
using Kwestie.Infrastructure.Persistence;
using Kwestie.IntegrationTests.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Kwestie.IntegrationTests.Workspaces;

public sealed class WorkspaceHttpTests(AuthenticationApiFactory factory)
    : IClassFixture<AuthenticationApiFactory>
{
    private const string Password = "Workspace-HTTP-Test1!";
    private readonly AuthenticationApiFactory _factory = factory;

    [Theory]
    [InlineData("GET", false)]
    [InlineData("POST", false)]
    [InlineData("GET", true)]
    public async Task Endpoints_WithoutBearer_ReturnUnauthorized(string method, bool individual)
    {
        using var client = _factory.CreateHttpsClient();
        var path = individual ? $"/api/workspaces/{Guid.NewGuid()}" : "/api/workspaces";
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = JsonContent.Create(new CreateWorkspaceRequest("Support"));

        using var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Endpoints_WithSignedTokenButInvalidSub_ReturnUnauthorized(string? subject)
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SignedToken(subject));

        using var get = await client.GetAsync("/api/workspaces");
        using var post = await client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest("Support"));
        using var detail = await client.GetAsync($"/api/workspaces/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, get.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, post.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, detail.StatusCode);
    }

    [Fact]
    public async Task Get_WithInvalidBearer_ReturnsUnauthorized()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not-a-jwt");

        using var response = await client.GetAsync($"/api/workspaces/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    public async Task Get_ActiveMember_ReturnsOnlyRequestedWorkspace_AndRepositoryDoesNotTrack(WorkspaceRole role)
    {
        using var client = _factory.CreateHttpsClient();
        using var otherClient = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        var otherEmail = UniqueEmail();
        var createdAt = new DateTimeOffset(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
        var workspace = new Workspace(Guid.NewGuid(), "Development", createdAt);
        var otherWorkspace = new Workspace(Guid.NewGuid(), "Other user's workspace", createdAt);
        var workspaceIds = new List<Guid> { workspace.Id, otherWorkspace.Id };
        try
        {
            var userId = await AuthenticateAsync(client, email);
            var otherUserId = await AuthenticateAsync(otherClient, otherEmail);
            await using var scope = _factory.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>();
            var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
            await repository.AddAsync(workspace, new WorkspaceMember(workspace.Id, userId, role, createdAt));
            await repository.AddAsync(otherWorkspace,
                new WorkspaceMember(otherWorkspace.Id, otherUserId, WorkspaceRole.Admin, createdAt));
            context.ChangeTracker.Clear();
            var stored = await repository.GetForUserAsync(workspace.Id, userId);
            Assert.NotNull(stored);
            Assert.Equal(workspace.Id, stored.Id);
            Assert.Empty(context.ChangeTracker.Entries());

            client.DefaultRequestHeaders.Add("UserId", otherUserId.ToString());
            client.DefaultRequestHeaders.Add("WorkspaceId", otherWorkspace.Id.ToString());
            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"/api/workspaces/{workspace.Id}?userId={otherUserId}&workspaceId={otherWorkspace.Id}")
            {
                Content = JsonContent.Create(new { userId = otherUserId })
            };
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var result = await response.Content.ReadFromJsonAsync<WorkspaceResponse>();
            Assert.Equal(new WorkspaceResponse(workspace.Id, workspace.Name, workspace.CreatedAt), result);
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(new[] { "createdAt", "name", "workspaceId" },
                json.RootElement.EnumerateObject().Select(p => p.Name).Order().ToArray());
            Assert.DoesNotContain(otherWorkspace.Name, await response.Content.ReadAsStringAsync());

            using var foreignResponse = await client.GetAsync($"/api/workspaces/{otherWorkspace.Id}");
            Assert.Equal(HttpStatusCode.NotFound, foreignResponse.StatusCode);
            Assert.Equal(string.Empty, await foreignResponse.Content.ReadAsStringAsync());
        }
        finally
        {
            await CleanupAsync([email, otherEmail], workspaceIds);
        }
    }

    [Fact]
    public async Task Get_UnavailableCases_ReturnIdenticalNotFound_AndMalformedIdReturnsBadRequest()
    {
        using var client = _factory.CreateHttpsClient();
        using var otherClient = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        var otherEmail = UniqueEmail();
        var createdAt = DateTimeOffset.UtcNow;
        var inactiveWorkspace = new Workspace(Guid.NewGuid(), "Inactive", createdAt);
        var foreignWorkspace = new Workspace(Guid.NewGuid(), "Foreign", createdAt);
        var workspaceIds = new List<Guid> { inactiveWorkspace.Id, foreignWorkspace.Id };
        try
        {
            var userId = await AuthenticateAsync(client, email);
            var otherUserId = await AuthenticateAsync(otherClient, otherEmail);
            await using var scope = _factory.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>();
            var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
            await repository.AddAsync(inactiveWorkspace,
                new WorkspaceMember(inactiveWorkspace.Id, userId, WorkspaceRole.Admin, createdAt));
            await repository.AddAsync(foreignWorkspace,
                new WorkspaceMember(foreignWorkspace.Id, otherUserId, WorkspaceRole.Admin, createdAt));
            await context.WorkspaceMembers.Where(m => m.WorkspaceId == inactiveWorkspace.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.IsActive, false));
            var missingId = Guid.NewGuid();
            Assert.False(await context.Workspaces.AnyAsync(w => w.Id == missingId));
            var responses = new List<(HttpStatusCode Status, string? ContentType, string Body)>();
            foreach (var workspaceId in new[] { foreignWorkspace.Id, inactiveWorkspace.Id, missingId })
            {
                using var response = await client.GetAsync($"/api/workspaces/{workspaceId}");
                responses.Add((response.StatusCode, response.Content.Headers.ContentType?.ToString(),
                    await response.Content.ReadAsStringAsync()));
            }
            Assert.All(responses, response =>
            {
                Assert.Equal(HttpStatusCode.NotFound, response.Status);
                Assert.Equal(string.Empty, response.Body);
                Assert.Equal(responses[0], response);
            });
            using var malformedResponse = await client.GetAsync("/api/workspaces/not-a-guid");
            Assert.Equal(HttpStatusCode.BadRequest, malformedResponse.StatusCode);
            Assert.Equal("application/problem+json", malformedResponse.Content.Headers.ContentType?.MediaType);
        }
        finally
        {
            await CleanupAsync([email, otherEmail], workspaceIds);
        }
    }

    [Fact]
    public async Task Create_PersistsWorkspaceAndAdminForJwtSubject_RejectsBodyUserId()
    {
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        var workspaceIds = new List<Guid>();
        var otherUserId = Guid.NewGuid();
        try
        {
            var userId = await AuthenticateAsync(client, email);
            using var emptyResponse = await client.GetAsync("/api/workspaces");
            Assert.Equal(HttpStatusCode.OK, emptyResponse.StatusCode);
            Assert.Empty((await emptyResponse.Content.ReadFromJsonAsync<WorkspaceResponse[]>())!);

            // Query/header values cannot replace the identity in the validated JWT.
            client.DefaultRequestHeaders.Add("UserId", otherUserId.ToString());
            using var created = await client.PostAsJsonAsync($"/api/workspaces?userId={otherUserId}",
                new CreateWorkspaceRequest("  Support team  "));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            Assert.Null(created.Headers.Location);
            var result = await created.Content.ReadFromJsonAsync<CreateWorkspaceResponse>();
            Assert.NotNull(result);
            Assert.NotEqual(Guid.Empty, result.WorkspaceId);
            workspaceIds.Add(result.WorkspaceId);
            using var json = JsonDocument.Parse(await created.Content.ReadAsStringAsync());
            Assert.Equal(["workspaceId"], json.RootElement.EnumerateObject().Select(p => p.Name).ToArray());

            await using var scope = _factory.Services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
            Assert.False(context.Database.HasPendingModelChanges());
            var workspace = await context.Workspaces.AsNoTracking().SingleAsync(w => w.Id == result.WorkspaceId);
            var member = await context.WorkspaceMembers.AsNoTracking().SingleAsync(m => m.WorkspaceId == workspace.Id);
            Assert.Equal("Support team", workspace.Name);
            Assert.Equal(userId, member.UserId);
            Assert.Equal(WorkspaceRole.Admin, member.Role);
            Assert.True(member.IsActive);
            Assert.Equal(workspace.CreatedAt, member.JoinedAt);

            using var spoofed = await client.PostAsJsonAsync("/api/workspaces",
                new { name = "Spoofed", userId = otherUserId });
            Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);
            Assert.Equal(1, await context.WorkspaceMembers.CountAsync(m => m.UserId == userId));
        }
        finally
        {
            await CleanupAsync([email], workspaceIds);
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t ")]
    public async Task Create_InvalidName_ReturnsBadRequestWithoutPersistence(string? name)
    {
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        try
        {
            var userId = await AuthenticateAsync(client, email);
            using var response = await client.PostAsJsonAsync("/api/workspaces", new CreateWorkspaceRequest(name));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            await using var scope = _factory.Services.CreateAsyncScope();
            Assert.False(await scope.ServiceProvider.GetRequiredService<KwestieDbContext>()
                .WorkspaceMembers.AnyAsync(m => m.UserId == userId));
        }
        finally
        {
            await CleanupAsync([email], []);
        }
    }

    [Fact]
    public async Task List_IsolatesUsers_FiltersInactiveMemberships_OrdersAndDoesNotTrack()
    {
        using var firstClient = _factory.CreateHttpsClient();
        using var secondClient = _factory.CreateHttpsClient();
        var firstEmail = UniqueEmail();
        var secondEmail = UniqueEmail();
        var workspaceIds = new List<Guid>();
        try
        {
            var firstUserId = await AuthenticateAsync(firstClient, firstEmail);
            var secondUserId = await AuthenticateAsync(secondClient, secondEmail);
            var createdAt = new DateTimeOffset(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);
            // Same random prefix, distinct final bytes: a stable Guid tie-breaker in SQL Server.
            var idBytes = Guid.NewGuid().ToByteArray();
            idBytes[15] = 1;
            var lowerId = new Guid(idBytes);
            idBytes[15] = 2;
            var higherId = new Guid(idBytes);
            var earlier = new Workspace(Guid.NewGuid(), "Earlier", createdAt.AddHours(-1));
            var lower = new Workspace(lowerId, "Lower ID", createdAt);
            var higher = new Workspace(higherId, "Higher ID", createdAt);
            var inactive = new Workspace(Guid.NewGuid(), "Inactive", createdAt.AddHours(-2));
            var otherUser = new Workspace(Guid.NewGuid(), "Other user", createdAt);

            await using var scope = _factory.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>();
            var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
            foreach (var workspace in new[] { higher, inactive, earlier, lower, otherUser })
            {
                workspaceIds.Add(workspace.Id);
                var userId = workspace == otherUser ? secondUserId : firstUserId;
                await repository.AddAsync(workspace,
                    new WorkspaceMember(workspace.Id, userId, WorkspaceRole.Member, workspace.CreatedAt));
            }
            // Database fixture only; this does not add membership-changing behavior to Domain/API.
            await context.WorkspaceMembers.Where(m => m.WorkspaceId == inactive.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.IsActive, false));
            context.ChangeTracker.Clear();
            await repository.ListForUserAsync(firstUserId);
            Assert.Empty(context.ChangeTracker.Entries());

            firstClient.DefaultRequestHeaders.Add("UserId", secondUserId.ToString());
            using var firstResponse = await firstClient.GetAsync($"/api/workspaces?userId={secondUserId}");
            using var secondResponse = await secondClient.GetAsync("/api/workspaces");
            Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
            Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
            var firstList = await firstResponse.Content.ReadFromJsonAsync<WorkspaceResponse[]>();
            var secondList = await secondResponse.Content.ReadFromJsonAsync<WorkspaceResponse[]>();
            Assert.NotNull(firstList);
            Assert.NotNull(secondList);
            Assert.Equal(new[] { earlier, lower, higher }.Select(w => new WorkspaceResponse(w.Id, w.Name, w.CreatedAt)), firstList);
            Assert.Equal(new WorkspaceResponse(otherUser.Id, otherUser.Name, otherUser.CreatedAt), Assert.Single(secondList));
            using var json = JsonDocument.Parse(await firstResponse.Content.ReadAsStringAsync());
            Assert.All(json.RootElement.EnumerateArray(), item =>
                Assert.Equal(new[] { "createdAt", "name", "workspaceId" },
                    item.EnumerateObject().Select(p => p.Name).Order().ToArray()));
        }
        finally
        {
            await CleanupAsync([firstEmail, secondEmail], workspaceIds);
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

    private string SignedToken(string? subject)
    {
        var options = _factory.Services.GetRequiredService<IOptions<JwtOptions>>().Value;
        var claims = new List<Claim>();
        if (subject is not null)
            claims.Add(new Claim("sub", subject));
        return new JwtSecurityTokenHandler().CreateEncodedJwt(new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = options.Issuer,
            Audience = options.Audience,
            IssuedAt = DateTime.UtcNow,
            NotBefore = DateTime.UtcNow,
            Expires = DateTime.UtcNow.AddMinutes(5),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Key)), SecurityAlgorithms.HmacSha256)
        });
    }

    private async Task CleanupAsync(string[] emails, List<Guid> workspaceIds)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
        // Include any row created before an assertion could record its returned ID.
        var userIds = await context.Users.Where(u => emails.Contains(u.Email!)).Select(u => u.Id).ToArrayAsync();
        var membershipWorkspaceIds = await context.WorkspaceMembers.Where(m => userIds.Contains(m.UserId))
            .Select(m => m.WorkspaceId).ToArrayAsync();
        var ownedWorkspaceIds = workspaceIds.Concat(membershipWorkspaceIds).Distinct().ToArray();
        await context.Workspaces.Where(w => ownedWorkspaceIds.Contains(w.Id)).ExecuteDeleteAsync();
        await context.Users.Where(u => userIds.Contains(u.Id)).ExecuteDeleteAsync();
        Assert.False(await context.Workspaces.AnyAsync(w => ownedWorkspaceIds.Contains(w.Id)));
        Assert.False(await context.WorkspaceMembers.AnyAsync(m => userIds.Contains(m.UserId)));
        Assert.False(await context.Users.AnyAsync(u => userIds.Contains(u.Id)));
        Assert.False(await context.RefreshTokens.AnyAsync(t => userIds.Contains(t.UserId)));
    }

    private static string UniqueEmail() => $"WorkspaceHttp-{Guid.NewGuid():N}@example.com";
}
