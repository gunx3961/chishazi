namespace Chishazi.Models;

public sealed record GoogleAccessToken
{
    public required string AccessToken { get; init; }

    public int ExpiresInSeconds { get; init; }
}
