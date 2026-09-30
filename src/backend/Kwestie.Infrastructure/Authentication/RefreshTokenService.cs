using System.Security.Cryptography;
using System.Text;
using Kwestie.Application.Authentication.Tokens;
using Kwestie.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Kwestie.Infrastructure.Authentication;

public sealed class RefreshTokenService(
    KwestieDbContext context, IOptions<RefreshTokenOptions> options, TimeProvider timeProvider) : IRefreshTokenService
{
    private readonly KwestieDbContext _context = context;
    private readonly RefreshTokenOptions _options = options.Value;
    private readonly TimeProvider _timeProvider = timeProvider;

    public async Task<RefreshTokenResult> IssueAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A refresh token requires a non-empty user ID.", nameof(userId));
        cancellationToken.ThrowIfCancellationRequested();

        var (raw, stored) = CreateToken(userId, _timeProvider.GetUtcNow());
        _context.RefreshTokens.Add(stored);
        await _context.SaveChangesAsync(cancellationToken);
        return new RefreshTokenResult(raw, stored.ExpiresAtUtc);
    }

    public async Task<RefreshTokenRotationResult> RotateAsync(
        string? refreshToken, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsWellFormed(refreshToken))
            return RefreshTokenRotationResult.InvalidToken();

        var hash = Hash(refreshToken!);
        var current = await _context.RefreshTokens.SingleOrDefaultAsync(token => token.TokenHash == hash, cancellationToken);
        var now = _timeProvider.GetUtcNow();
        if (current is null || current.RevokedAtUtc.HasValue || current.ExpiresAtUtc <= now)
            return RefreshTokenRotationResult.InvalidToken();

        var (raw, replacement) = CreateToken(current.UserId, now);
        current.Revoke(now);
        _context.RefreshTokens.Add(replacement);
        try
        {
            // EF's single SaveChanges transaction makes revocation + insertion atomic.
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception) when (
            exception.Entries.Count == 1 && ReferenceEquals(exception.Entries[0].Entity, current))
        {
            // A failed SaveChanges rolled back the insertion. Do not leave it queued for a later save.
            _context.Entry(replacement).State = EntityState.Detached;
            var databaseValues = await _context.Entry(current).GetDatabaseValuesAsync(cancellationToken);
            _context.Entry(current).State = EntityState.Detached;
            if (databaseValues is null || databaseValues.GetValue<DateTimeOffset?>(nameof(RefreshToken.RevokedAtUtc)).HasValue)
                return RefreshTokenRotationResult.InvalidToken();

            // A different concurrent modification is not a normal consumed-token rejection.
            throw;
        }

        return RefreshTokenRotationResult.Success(current.UserId, raw, replacement.ExpiresAtUtc);
    }

    private (string Raw, RefreshToken Stored) CreateToken(Guid userId, DateTimeOffset now)
    {
        var raw = WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var stored = new RefreshToken(Guid.NewGuid(), userId, Hash(raw), now, now.AddDays(_options.LifetimeDays));
        return (raw, stored);
    }

    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    private static bool IsWellFormed(string? token)
    {
        // Exactly the canonical, unpadded Base64Url representation of 32 random bytes.
        if (token is null || token.Length != 43 || token.Any(character =>
            !char.IsAsciiLetterOrDigit(character) && character != '-' && character != '_'))
            return false;

        var bytes = new byte[32];
        var base64 = token.Replace('-', '+').Replace('_', '/') + "=";
        return Convert.TryFromBase64String(base64, bytes, out var written)
            && written == 32 && WebEncoders.Base64UrlEncode(bytes) == token;
    }
}
