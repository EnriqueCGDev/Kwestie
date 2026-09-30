namespace Kwestie.Application.Authentication.Tokens;

public interface IRefreshTokenService
{
    Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<RefreshTokenRotationResult> RotateAsync(string? refreshToken, CancellationToken cancellationToken = default);
    Task RevokeAsync(string? refreshToken, CancellationToken cancellationToken = default);
}
