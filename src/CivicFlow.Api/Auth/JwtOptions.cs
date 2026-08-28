namespace CivicFlow.Api.Auth;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = "CivicFlow";
    public string Audience { get; init; } = "CivicFlow.Client";
    public string Key { get; init; } = string.Empty;
    public int AccessTokenMinutes { get; init; } = 30;
    public int RefreshTokenDays { get; init; } = 7;
}
