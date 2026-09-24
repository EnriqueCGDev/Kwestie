using Kwestie.Domain.Common;

namespace Kwestie.Domain.Kwesties;

public class Kwestie
{
    public Guid Id { get; }
    public long Number { get; private set; }
    public Guid WorkspaceId { get; }
    public string Title { get; }
    public string Description { get; }
    public KwestieStatus Status { get; private set; }
    public KwestiePriority Priority { get; }
    public Guid CreatedById { get; }
    public Guid? AssignedToId { get; }
    public Guid? CategoryId { get; }
    public DateTimeOffset CreatedAt { get; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }

    public Kwestie(
        Guid id,
        Guid workspaceId,
        string title,
        string? description,
        KwestiePriority priority,
        Guid createdById,
        DateTimeOffset createdAt,
        Guid? categoryId = null)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("A kwestie ID must not be empty.");
        }

        if (workspaceId == Guid.Empty)
        {
            throw new DomainException("A workspace ID must not be empty.");
        }

        if (createdById == Guid.Empty)
        {
            throw new DomainException("A creator ID must not be empty.");
        }

        if (categoryId == Guid.Empty)
        {
            throw new DomainException("A category ID must not be empty when provided.");
        }

        if (!Enum.IsDefined(priority))
        {
            throw new DomainException("A kwestie priority must be a defined value.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("A kwestie title is required.");
        }

        Id = id;
        WorkspaceId = workspaceId;
        Title = title.Trim();
        Description = description?.Trim() ?? string.Empty;
        Status = KwestieStatus.Open;
        Priority = priority;
        CreatedById = createdById;
        CategoryId = categoryId;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public void StartProgress(DateTimeOffset occurredAt)
    {
        if (Status != KwestieStatus.Open)
        {
            throw new DomainException("Only an open kwestie can start progress.");
        }

        Status = KwestieStatus.InProgress;
        UpdatedAt = occurredAt;
    }

    public void Resolve(DateTimeOffset occurredAt)
    {
        if (Status != KwestieStatus.InProgress)
        {
            throw new DomainException("Only a kwestie in progress can be resolved.");
        }

        Status = KwestieStatus.Resolved;
        ResolvedAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    public void Close(DateTimeOffset occurredAt)
    {
        if (Status != KwestieStatus.Resolved)
        {
            throw new DomainException("Only a resolved kwestie can be closed.");
        }

        Status = KwestieStatus.Closed;
        ClosedAt = occurredAt;
        UpdatedAt = occurredAt;
    }
}
