namespace Kwestie.Infrastructure.Authentication;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshTokens";
    public int LifetimeDays { get; set; }
}
