using Kwestie.Domain.Common;
using Kwestie.Domain.Workspaces;

namespace Kwestie.Domain.Tests.Workspaces;

public class WorkspaceMemberTests
{
    private static readonly DateTimeOffset JoinedAt =
        new(2026, 10, 6, 10, 0, 0, TimeSpan.FromHours(-6));

    [Theory]
    [InlineData(WorkspaceRole.Admin)]
    [InlineData(WorkspaceRole.Member)]
    public void Constructor_InitializesActiveMembership(WorkspaceRole role)
    {
        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        var member = new WorkspaceMember(workspaceId, userId, role, JoinedAt);

        Assert.Equal(workspaceId, member.WorkspaceId);
        Assert.Equal(userId, member.UserId);
        Assert.Equal(role, member.Role);
        Assert.Equal(JoinedAt, member.JoinedAt);
        Assert.True(member.IsActive);
    }

    [Fact]
    public void Constructor_EmptyWorkspaceId_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new WorkspaceMember(
            Guid.Empty, Guid.NewGuid(), WorkspaceRole.Member, JoinedAt));
    }

    [Fact]
    public void Constructor_EmptyUserId_ThrowsDomainException()
    {
        Assert.Throws<DomainException>(() => new WorkspaceMember(
            Guid.NewGuid(), Guid.Empty, WorkspaceRole.Member, JoinedAt));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(3)]
    public void Constructor_UndefinedRole_ThrowsDomainException(int role)
    {
        Assert.Throws<DomainException>(() => new WorkspaceMember(
            Guid.NewGuid(), Guid.NewGuid(), (WorkspaceRole)role, JoinedAt));
    }
}
