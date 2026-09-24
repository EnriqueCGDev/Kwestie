using Kwestie.Domain.Common;
using Kwestie.Domain.Kwesties;
using KwestieEntity = Kwestie.Domain.Kwesties.Kwestie;

namespace Kwestie.Domain.Tests.Kwesties;

public class KwestieTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 9, 22, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void Constructor_InitializesPropertiesAndTrimsDetails()
    {
        var id = Guid.NewGuid();
        var workspaceId = Guid.NewGuid();
        var createdById = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var kwestie = new KwestieEntity(
            id, workspaceId, "  Broken printer  ", "  Cannot print.  ",
            KwestiePriority.High, createdById, CreatedAt, categoryId);

        Assert.Equal(id, kwestie.Id);
        Assert.Equal(workspaceId, kwestie.WorkspaceId);
        Assert.Equal(createdById, kwestie.CreatedById);
        Assert.Equal(categoryId, kwestie.CategoryId);
        Assert.Equal("Broken printer", kwestie.Title);
        Assert.Equal("Cannot print.", kwestie.Description);
        Assert.Equal(KwestieStatus.Open, kwestie.Status);
        Assert.Equal(CreatedAt, kwestie.CreatedAt);
        Assert.Equal(CreatedAt, kwestie.UpdatedAt);
        Assert.Null(kwestie.AssignedToId);
        Assert.Null(kwestie.ResolvedAt);
        Assert.Null(kwestie.ClosedAt);
    }

    [Theory]
    [InlineData(KwestiePriority.Low)]
    [InlineData(KwestiePriority.Normal)]
    [InlineData(KwestiePriority.High)]
    [InlineData(KwestiePriority.Critical)]
    public void Constructor_PreservesPriority(KwestiePriority priority)
    {
        Assert.Equal(priority, CreateKwestie(priority: priority).Priority);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_InvalidTitle_ThrowsDomainException(string? title)
    {
        Assert.Throws<DomainException>(() => CreateKwestie(title: title!));
    }

    [Fact]
    public void Constructor_WithoutCategory_LeavesCategoryUnset()
    {
        Assert.Null(CreateKwestie().CategoryId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyDescription_StoresEmptyString(string? description)
    {
        Assert.Equal(string.Empty, CreateKwestie(description: description).Description);
    }

    [Fact]
    public void StartProgress_FromOpen_UpdatesStatusAndTimestamp()
    {
        var kwestie = CreateKwestie();
        var occurredAt = CreatedAt.AddHours(1);

        kwestie.StartProgress(occurredAt);

        Assert.Equal(KwestieStatus.InProgress, kwestie.Status);
        Assert.Equal(occurredAt, kwestie.UpdatedAt);
        Assert.Equal(CreatedAt, kwestie.CreatedAt);
        Assert.Null(kwestie.ResolvedAt);
        Assert.Null(kwestie.ClosedAt);
    }

    [Fact]
    public void Resolve_FromInProgress_SetsResolutionTimestamp()
    {
        var kwestie = CreateKwestie();
        kwestie.StartProgress(CreatedAt.AddHours(1));
        var occurredAt = CreatedAt.AddHours(2);

        kwestie.Resolve(occurredAt);

        Assert.Equal(KwestieStatus.Resolved, kwestie.Status);
        Assert.Equal(occurredAt, kwestie.ResolvedAt);
        Assert.Equal(occurredAt, kwestie.UpdatedAt);
        Assert.Equal(CreatedAt, kwestie.CreatedAt);
        Assert.Null(kwestie.ClosedAt);
    }

    [Fact]
    public void Close_FromResolved_SetsClosureTimestampAndPreservesResolutionTimestamp()
    {
        var kwestie = CreateKwestie();
        kwestie.StartProgress(CreatedAt.AddHours(1));
        var resolvedAt = CreatedAt.AddHours(2);
        kwestie.Resolve(resolvedAt);
        var occurredAt = CreatedAt.AddHours(3);

        kwestie.Close(occurredAt);

        Assert.Equal(KwestieStatus.Closed, kwestie.Status);
        Assert.Equal(occurredAt, kwestie.ClosedAt);
        Assert.Equal(occurredAt, kwestie.UpdatedAt);
        Assert.Equal(resolvedAt, kwestie.ResolvedAt);
        Assert.Equal(CreatedAt, kwestie.CreatedAt);
    }

    [Theory]
    [InlineData(KwestieStatus.InProgress, nameof(KwestieEntity.StartProgress))]
    [InlineData(KwestieStatus.Resolved, nameof(KwestieEntity.StartProgress))]
    [InlineData(KwestieStatus.Closed, nameof(KwestieEntity.StartProgress))]
    [InlineData(KwestieStatus.Open, nameof(KwestieEntity.Resolve))]
    [InlineData(KwestieStatus.Resolved, nameof(KwestieEntity.Resolve))]
    [InlineData(KwestieStatus.Closed, nameof(KwestieEntity.Resolve))]
    [InlineData(KwestieStatus.Open, nameof(KwestieEntity.Close))]
    [InlineData(KwestieStatus.InProgress, nameof(KwestieEntity.Close))]
    [InlineData(KwestieStatus.Closed, nameof(KwestieEntity.Close))]
    public void Transition_FromInvalidStatus_ThrowsWithoutChangingState(
        KwestieStatus status, string transition)
    {
        var kwestie = CreateKwestie();
        if (status != KwestieStatus.Open)
            kwestie.StartProgress(CreatedAt.AddHours(1));
        if (status is KwestieStatus.Resolved or KwestieStatus.Closed)
            kwestie.Resolve(CreatedAt.AddHours(2));
        if (status == KwestieStatus.Closed)
            kwestie.Close(CreatedAt.AddHours(3));

        var updatedAt = kwestie.UpdatedAt;
        var resolvedAt = kwestie.ResolvedAt;
        var closedAt = kwestie.ClosedAt;
        Action<DateTimeOffset> action = transition switch
        {
            nameof(KwestieEntity.StartProgress) => kwestie.StartProgress,
            nameof(KwestieEntity.Resolve) => kwestie.Resolve,
            nameof(KwestieEntity.Close) => kwestie.Close,
            _ => throw new ArgumentOutOfRangeException(nameof(transition))
        };

        Assert.Throws<DomainException>(() => action(CreatedAt.AddHours(4)));

        Assert.Equal(status, kwestie.Status);
        Assert.Equal(updatedAt, kwestie.UpdatedAt);
        Assert.Equal(resolvedAt, kwestie.ResolvedAt);
        Assert.Equal(closedAt, kwestie.ClosedAt);
        Assert.Equal(CreatedAt, kwestie.CreatedAt);
    }

    private static KwestieEntity CreateKwestie(
        string title = "Broken printer",
        string? description = "Cannot print.",
        KwestiePriority priority = KwestiePriority.Normal)
    {
        return new KwestieEntity(
            Guid.NewGuid(), Guid.NewGuid(), title, description,
            priority, Guid.NewGuid(), CreatedAt);
    }
}
