namespace SkillSnap.Client.Services;

public sealed class AccessTokenStore
{
    private string? _accessToken;
    private DateTimeOffset? _expiresAt;

    public string? GetValidAccessToken()
    {
        return !string.IsNullOrWhiteSpace(_accessToken)
            && _expiresAt is { } expiresAt
            && expiresAt > DateTimeOffset.UtcNow
                ? _accessToken
                : null;
    }

    public void Set(string? accessToken, DateTimeOffset? expiresAt)
    {
        _accessToken = accessToken;
        _expiresAt = expiresAt;
    }
}
