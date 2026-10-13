using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Kwestie.Api.Contracts.Kwesties;
using Kwestie.Application.Kwesties;
using Kwestie.Application.Workspaces;
using Kwestie.Domain.Kwesties;
using Kwestie.Domain.Workspaces;
using Kwestie.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.IntegrationTests.Kwesties;

public sealed partial class KwestieHttpTests
{
    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    public async Task Transitions_ActiveRole_PersistsDomainDatesAndRejectsInvalidStatesWithoutWrites(WorkspaceRole role)
    {
        await RequireTransitionMigrationAsync();
        var clock = new TransitionClock();
        using var factory = TransitionFactory(clock);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var email = UniqueEmail();
        var workspaces = new List<Guid>();
        try
        {
            var userId = await AuthenticateAsync(client, email);
            var workspaceId = await CreateWorkspaceAsync(client, workspaces);
            using var created = await client.PostAsJsonAsync(Path(workspaceId), ValidRequest());
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var id = (await created.Content.ReadFromJsonAsync<CreateKwestieResponse>())!.KwestieId;
            if (role == WorkspaceRole.Member)
            {
                await using var scope = factory.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<KwestieDbContext>().WorkspaceMembers
                    .Where(m => m.WorkspaceId == workspaceId && m.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.Role, WorkspaceRole.Member));
            }
            var initial = await ReadTransitionEntityAsync(workspaceId, id);
            await AssertRejectedTransitionAsync(client, workspaceId, id, "resolve", HttpStatusCode.Conflict);
            await AssertRejectedTransitionAsync(client, workspaceId, id, "close",
                role == WorkspaceRole.Admin ? HttpStatusCode.Conflict : HttpStatusCode.Forbidden);
            clock.Advance();
            await AssertAppliedTransitionAsync(client, workspaceId, id, "start-progress");
            var progressing = await ReadTransitionEntityAsync(workspaceId, id);
            Assert.Equal(KwestieStatus.InProgress, progressing.Status);
            Assert.Equal(clock.GetUtcNow(), progressing.UpdatedAt);
            Assert.Null(progressing.ResolvedAt);
            Assert.Null(progressing.ClosedAt);
            await AssertRejectedTransitionAsync(client, workspaceId, id, "start-progress", HttpStatusCode.Conflict);
            clock.Advance();
            await AssertAppliedTransitionAsync(client, workspaceId, id, "resolve");
            var resolved = await ReadTransitionEntityAsync(workspaceId, id);
            Assert.Equal(KwestieStatus.Resolved, resolved.Status);
            Assert.Equal(clock.GetUtcNow(), resolved.UpdatedAt);
            Assert.Equal(clock.GetUtcNow(), resolved.ResolvedAt);
            Assert.Null(resolved.ClosedAt);
            await AssertRejectedTransitionAsync(client, workspaceId, id, "resolve", HttpStatusCode.Conflict);
            clock.Advance();
            if (role == WorkspaceRole.Admin)
            {
                await AssertAppliedTransitionAsync(client, workspaceId, id, "close");
                var closed = await ReadTransitionEntityAsync(workspaceId, id);
                Assert.Equal(KwestieStatus.Closed, closed.Status);
                Assert.Equal(clock.GetUtcNow(), closed.UpdatedAt);
                Assert.Equal(clock.GetUtcNow(), closed.ClosedAt);
                Assert.Equal(resolved.ResolvedAt, closed.ResolvedAt);
                foreach (var action in TransitionActions)
                    await AssertRejectedTransitionAsync(client, workspaceId, id, action, HttpStatusCode.Conflict);
            }
            else
            {
                // Unbound client identity/role values must not override the stored membership or route.
                var before = await TransitionSnapshotAsync(workspaceId, id);
                client.DefaultRequestHeaders.Add("Role", "Admin");
                client.DefaultRequestHeaders.Add("UserId", Guid.NewGuid().ToString());
                using var response = await client.PostAsJsonAsync(
                    $"{DetailPath(workspaceId, id)}/close?role=Admin&userId={Guid.NewGuid()}",
                    new { role = "Admin", workspaceId = Guid.NewGuid(), kwestieId = Guid.NewGuid() });
                Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
                Assert.Equal(before, await TransitionSnapshotAsync(workspaceId, id));
            }
            var final = await ReadTransitionEntityAsync(workspaceId, id);
            Assert.Equal(initial.Id, final.Id);
            Assert.Equal(initial.Number, final.Number);
            Assert.Equal(initial.CreatedAt, final.CreatedAt);
            Assert.Equal(initial.CreatedById, final.CreatedById);
            Assert.Equal(initial.Title, final.Title);
            Assert.Equal(initial.Description, final.Description);
            Assert.Equal(initial.Priority, final.Priority);
            Assert.Equal(initial.WorkspaceId, final.WorkspaceId);
            Assert.Null(final.AssignedToId);
            Assert.Null(final.CategoryId);
        }
        finally { await CleanupAsync([email], workspaces); }
    }

    [Fact]
    public async Task Transitions_UnavailableResources_ReturnIdenticalEmpty404WithoutChangingRows()
    {
        await RequireTransitionMigrationAsync();
        using var client = _factory.CreateHttpsClient();
        using var otherClient = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        var otherEmail = UniqueEmail();
        var workspaces = new List<Guid>();
        try
        {
            var userId = await AuthenticateAsync(client, email);
            var otherUserId = await AuthenticateAsync(otherClient, otherEmail);
            var ownWorkspace = await CreateWorkspaceAsync(client, workspaces);
            var inactiveWorkspace = await CreateWorkspaceAsync(client, workspaces);
            var foreignWorkspace = await CreateWorkspaceAsync(otherClient, workspaces);
            var own = TransitionEntity(ownWorkspace, userId);
            var inactive = TransitionEntity(inactiveWorkspace, userId);
            var foreign = TransitionEntity(foreignWorkspace, otherUserId);
            await using (var scope = _factory.Services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
                context.Kwesties.AddRange(own, inactive, foreign);
                await context.SaveChangesAsync();
                await context.WorkspaceMembers.Where(m => m.WorkspaceId == ownWorkspace && m.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.Role, WorkspaceRole.Member));
                await context.WorkspaceMembers.Where(m => m.WorkspaceId == inactiveWorkspace && m.UserId == userId)
                    .ExecuteUpdateAsync(setters => setters.SetProperty(m => m.IsActive, false));
            }
            var before = await Task.WhenAll(new[] { own, inactive, foreign }.Select(k => TransitionSnapshotAsync(k.WorkspaceId, k.Id)));
            var responses = new List<(HttpStatusCode Status, string? Type, string Body)>();
            foreach (var (workspaceId, id) in new[]
            {
                (Guid.NewGuid(), own.Id), (foreignWorkspace, foreign.Id), (inactiveWorkspace, inactive.Id),
                (ownWorkspace, Guid.NewGuid()), (ownWorkspace, foreign.Id)
            })
                foreach (var action in TransitionActions)
                {
                    using var response = await client.PostAsync($"{DetailPath(workspaceId, id)}/{action}", null);
                    responses.Add((response.StatusCode, response.Content.Headers.ContentType?.ToString(),
                        await response.Content.ReadAsStringAsync()));
                }
            Assert.All(responses, response =>
            {
                Assert.Equal(HttpStatusCode.NotFound, response.Status);
                Assert.Equal(string.Empty, response.Body);
                Assert.Equal(responses[0], response);
            });
            Assert.Equal(before, await Task.WhenAll(new[] { own, inactive, foreign }.Select(k => TransitionSnapshotAsync(k.WorkspaceId, k.Id))));
        }
        finally { await CleanupAsync([email, otherEmail], workspaces); }
    }

    [Theory]
    [InlineData("start-progress", KwestieStatus.Open, KwestieStatus.InProgress)]
    [InlineData("resolve", KwestieStatus.InProgress, KwestieStatus.Resolved)]
    [InlineData("close", KwestieStatus.Resolved, KwestieStatus.Closed)]
    public async Task Transitions_ConcurrentIndependentRequests_OnlyOneCommitsAndTheOtherReturns409(
        string action, KwestieStatus initial, KwestieStatus expected)
    {
        await RequireTransitionMigrationAsync();
        var clock = new TransitionClock();
        var barrier = new TransitionSaveBarrier();
        using var factory = TransitionFactory(clock, barrier);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var email = UniqueEmail();
        var workspaces = new List<Guid>();
        try
        {
            var userId = await AuthenticateAsync(client, email);
            var workspaceId = await CreateWorkspaceAsync(client, workspaces);
            var entity = TransitionEntity(workspaceId, userId, initial);
            await using (var scope = factory.Services.CreateAsyncScope())
                await scope.ServiceProvider.GetRequiredService<IKwestieRepository>().AddAsync(entity);
            var before = await TransitionSnapshotAsync(workspaceId, entity.Id);
            clock.Advance();
            barrier.Enabled = true;
            var path = $"{DetailPath(workspaceId, entity.Id)}/{action}";
            var responses = await Task.WhenAll(client.PostAsync(path, null), client.PostAsync(path, null));
            try
            {
                Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.NoContent));
                Assert.Equal(1, responses.Count(r => r.StatusCode == HttpStatusCode.Conflict));
                foreach (var response in responses)
                    Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
                Assert.Equal(2, barrier.Arrivals);
            }
            finally { foreach (var response in responses) response.Dispose(); }
            var stored = await ReadTransitionEntityAsync(workspaceId, entity.Id);
            Assert.Equal(expected, stored.Status);
            Assert.Equal(clock.GetUtcNow(), stored.UpdatedAt);
            Assert.Equal(action == "resolve" ? clock.GetUtcNow() : entity.ResolvedAt, stored.ResolvedAt);
            Assert.Equal(action == "close" ? clock.GetUtcNow() : (DateTimeOffset?)null, stored.ClosedAt);
            Assert.NotEqual(before, await TransitionSnapshotAsync(workspaceId, entity.Id));
            await AssertRejectedTransitionAsync(client, workspaceId, entity.Id, action, HttpStatusCode.Conflict);
        }
        finally { await CleanupAsync([email], workspaces); }
    }

    [Fact]
    public async Task Transitions_MalformedRouteGuids_ReturnBinding400()
    {
        using var client = _factory.CreateHttpsClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", SignedToken(Guid.NewGuid().ToString()));
        foreach (var action in TransitionActions)
            foreach (var path in new[]
            {
                $"/api/workspaces/not-a-guid/kwesties/{Guid.NewGuid()}/{action}",
                $"{Path(Guid.NewGuid())}/not-a-guid/{action}",
                $"/api/workspaces/not-a-guid/kwesties/not-a-guid/{action}"
            })
            {
                using var response = await client.PostAsync(path, null);
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            }
    }

    [Fact]
    public async Task TransitionRepositories_Cancellation_DoesNotPersistTheDomainMutation()
    {
        await RequireTransitionMigrationAsync();
        using var client = _factory.CreateHttpsClient();
        var email = UniqueEmail();
        var workspaces = new List<Guid>();
        try
        {
            var userId = await AuthenticateAsync(client, email);
            var workspaceId = await CreateWorkspaceAsync(client, workspaces);
            var entity = TransitionEntity(workspaceId, userId);
            await using var scope = _factory.Services.CreateAsyncScope();
            var repository = scope.ServiceProvider.GetRequiredService<IKwestieRepository>();
            await repository.AddAsync(entity);
            var before = await TransitionSnapshotAsync(workspaceId, entity.Id);
            var tracked = await repository.GetForUpdateAsync(workspaceId, entity.Id);
            Assert.NotNull(tracked);
            Assert.Null(await repository.GetForUpdateAsync(Guid.NewGuid(), entity.Id));
            using var cancellation = new CancellationTokenSource();
            await cancellation.CancelAsync();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                scope.ServiceProvider.GetRequiredService<IWorkspaceRepository>()
                    .GetActiveRoleAsync(workspaceId, userId, cancellation.Token));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
                repository.GetForUpdateAsync(workspaceId, entity.Id, cancellation.Token));
            tracked.StartProgress(DateTimeOffset.UtcNow);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => repository.UpdateAsync(tracked, cancellation.Token));
            Assert.Equal(before, await TransitionSnapshotAsync(workspaceId, entity.Id));
        }
        finally { await CleanupAsync([email], workspaces); }
    }

    private static readonly string[] TransitionActions = ["start-progress", "resolve", "close"];

    private async Task RequireTransitionMigrationAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<KwestieDbContext>();
        Assert.False(context.Database.HasPendingModelChanges());
        if (!(await context.Database.GetAppliedMigrationsAsync()).Any(m => m.EndsWith("_AddKwestieRowVersion", StringComparison.Ordinal)))
            throw new InvalidOperationException("AddKwestieRowVersion must be applied manually before transition SQL tests. Tests never apply migrations.");
    }

    private WebApplicationFactory<Program> TransitionFactory(TransitionClock clock, TransitionSaveBarrier? barrier = null) =>
        _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(clock);
            if (barrier is not null)
                services.AddDbContext<KwestieDbContext>(options => options.AddInterceptors(barrier));
        }));

    private async Task<KwestieEntity> ReadTransitionEntityAsync(Guid workspaceId, Guid id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KwestieDbContext>().Kwesties.AsNoTracking()
            .SingleAsync(k => k.WorkspaceId == workspaceId && k.Id == id);
    }

    private async Task<string> TransitionSnapshotAsync(Guid workspaceId, Guid id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var row = await scope.ServiceProvider.GetRequiredService<KwestieDbContext>().Kwesties.AsNoTracking()
            .Where(k => k.WorkspaceId == workspaceId && k.Id == id)
            .Select(k => new { Entity = k, RowVersion = EF.Property<byte[]>(k, "RowVersion") }).SingleAsync();
        return JsonSerializer.Serialize(row);
    }

    private static async Task AssertAppliedTransitionAsync(HttpClient client, Guid workspaceId, Guid id, string action)
    {
        using var response = await client.PostAsync($"{DetailPath(workspaceId, id)}/{action}", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
    }

    private async Task AssertRejectedTransitionAsync(HttpClient client, Guid workspaceId, Guid id, string action, HttpStatusCode status)
    {
        var before = await TransitionSnapshotAsync(workspaceId, id);
        using var response = await client.PostAsync($"{DetailPath(workspaceId, id)}/{action}", null);
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(string.Empty, await response.Content.ReadAsStringAsync());
        Assert.Equal(before, await TransitionSnapshotAsync(workspaceId, id));
    }

    private static KwestieEntity TransitionEntity(Guid workspaceId, Guid userId, KwestieStatus state = KwestieStatus.Open)
    {
        var now = DateTimeOffset.UtcNow.AddMinutes(-5);
        var entity = new KwestieEntity(Guid.NewGuid(), workspaceId, "Transition test", "Persisted issue",
            KwestiePriority.Normal, userId, now);
        if (state >= KwestieStatus.InProgress) entity.StartProgress(now.AddMinutes(1));
        if (state >= KwestieStatus.Resolved) entity.Resolve(now.AddMinutes(2));
        return entity;
    }

    private sealed class TransitionClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance() => _now = _now.AddMinutes(1);
    }

    // Test-only barrier ensures two independent request contexts read the same version before either UPDATE.
    private sealed class TransitionSaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _arrivals;
        public bool Enabled { get; set; }
        public int Arrivals => Volatile.Read(ref _arrivals);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            eventData.Context!.ChangeTracker.DetectChanges();
            if (Enabled && eventData.Context.ChangeTracker.Entries<KwestieEntity>().Any(e => e.State == EntityState.Modified))
            {
                if (Interlocked.Increment(ref _arrivals) == 2) _ready.TrySetResult();
                await _ready.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            }
            return result;
        }
    }
}
