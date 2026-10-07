using Kwestie.Domain.Common;

namespace Kwestie.Domain.Workspaces;

public class Workspace
{
    public Guid Id { get; }
    public string Name { get; }
    public DateTimeOffset CreatedAt { get; }

    public Workspace(Guid id, string name, DateTimeOffset createdAt)
    {
        if (id == Guid.Empty)
        {
            throw new DomainException("A workspace ID must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("A workspace name is required.");
        }

        Id = id;
        Name = name.Trim();
        CreatedAt = createdAt;
    }
}
