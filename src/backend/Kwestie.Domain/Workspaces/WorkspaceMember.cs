using Kwestie.Domain.Common;

namespace Kwestie.Domain.Workspaces;

public class WorkspaceMember
{
    public Guid WorkspaceId { get; }
    public Guid UserId { get; }
    public WorkspaceRole Role { get; }
    public DateTimeOffset JoinedAt { get; }
    public bool IsActive { get; }

    public WorkspaceMember(
        Guid workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTimeOffset joinedAt)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new DomainException("A workspace ID must not be empty.");
        }

        if (userId == Guid.Empty)
        {
            throw new DomainException("A user ID must not be empty.");
        }

        if (!Enum.IsDefined(role))
        {
            throw new DomainException("A workspace role must be a defined value.");
        }

        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        JoinedAt = joinedAt;
        IsActive = true;
    }
}
