using Kwestie.Domain.Common;
using Kwestie.Domain.Workspaces;

namespace Kwestie.Domain.Tests.Workspaces;

public class WorkspaceTests
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 10, 6, 10, 0, 0, TimeSpan.FromHours(-6));

    [Fact]
    public void Constructor_InitializesProperties()
    {
        var id = Guid.NewGuid();

        var workspace = new Workspace(id, "Support", CreatedAt);

        Assert.Equal(id, workspace.Id);
        Assert.Equal("Support", workspace.Name);
        Assert.Equal(CreatedAt, workspace.CreatedAt);
    }

    [Fact]
    public void Constructor_TrimsName()
    {
        var workspace = new Workspace(Guid.NewGuid(), " \t Support team \r\n ", CreatedAt);

        Assert.Equal("Support team", workspace.Name);
    }

    [Fact]
    public void Constructor_EmptyId_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new Workspace(Guid.Empty, "Support", CreatedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\r\n")]
    public void Constructor_InvalidName_ThrowsDomainException(string? name)
    {
        Assert.Throws<DomainException>(() => new Workspace(Guid.NewGuid(), name!, CreatedAt));
    }
}
