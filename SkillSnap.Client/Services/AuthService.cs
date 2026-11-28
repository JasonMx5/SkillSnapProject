using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.JSInterop;

namespace SkillSnap.Client.Services;

public class AuthService(
    IHttpClientFactory httpClientFactory,
    IJSRuntime jsRuntime,
    UserSessionService userSessionService,
    AccessTokenStore accessTokenStore) : IAsyncDisposable
{
    private const string TokenStorageKey = "skillsnap.auth.token";
    private const string EmailStorageKey = "skillsnap.auth.email";
    private const string ExpirationStorageKey = "skillsnap.auth.expiresAt";
    private const string UserIdStorageKey = "skillsnap.auth.userId";
    private const string RoleStorageKey = "skillsnap.auth.role";
    private const string SessionVersionStorageKey = "skillsnap.auth.sessionVersion";
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("AuthApi");
    private readonly SemaphoreSlim _initializationLock = new(1, 1);
    private bool _initialized;
    private string? _accessToken;
    private string? _email;
    private DateTimeOffset? _expiresAt;
    private DotNetObjectReference<AuthService>? _selfReference;

    public bool IsAuthenticated =>
        !string.IsNullOrWhiteSpace(_accessToken)
        && _expiresAt is { } expiresAt
        && expiresAt > DateTimeOffset.UtcNow;

    public string? Email => IsAuthenticated ? _email : null;

    public event Action? AuthenticationStateChanged;

    public async Task InitializeAsync()
    {
        if (_initialized)
        {
            return;
        }

        await _initializationLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                return;
            }

            await SubscribeToStorageChangesAsync();
            var storedCredentials = await ReadStoredCredentialsAsync();
            await ApplyStoredCredentialsAsync(storedCredentials);

            if (IsValid(storedCredentials)
                && string.IsNullOrWhiteSpace(await jsRuntime.InvokeAsync<string?>(
                    "localStorage.getItem",
                    SessionVersionStorageKey)))
            {
                await WriteSessionVersionAsync();
            }

            _initialized = true;
            NotifyAuthenticationStateChanged();
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    [JSInvokable]
    public async Task SyncFromBrowserStorageAsync()
    {
        await _initializationLock.WaitAsync();
        try
        {
            var storedCredentials = await ReadStoredCredentialsAsync();
            var previousToken = _accessToken;
            var previousEmail = _email;
            var previousExpiration = _expiresAt;
            var previousUserId = userSessionService.UserId;
            var previousRole = userSessionService.Role;

            await ApplyStoredCredentialsAsync(storedCredentials);
            _initialized = true;

            if (previousToken != _accessToken
                || previousEmail != _email
                || previousExpiration != _expiresAt
                || previousUserId != userSessionService.UserId
                || previousRole != userSessionService.Role)
            {
                NotifyAuthenticationStateChanged();
            }
        }
        finally
        {
            _initializationLock.Release();
        }
    }

    public async Task<string?> GetAccessTokenAsync()
    {
        await InitializeAsync();

        if (_expiresAt is { } expiresAt && expiresAt <= DateTimeOffset.UtcNow)
        {
            await LogoutAsync();
            return null;
        }

        return _accessToken;
    }

    public async Task RegisterAsync(string email, string password)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/register", new AuthRequest(email, password));
        await EnsureSuccessAsync(response);
    }

    public async Task LoginAsync(string email, string password)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/auth/login", new AuthRequest(email, password));
        await EnsureSuccessAsync(response);

        var result = await response.Content.ReadFromJsonAsync<LoginResponse>()
            ?? throw new InvalidOperationException("The login endpoint returned an empty response.");

        if (string.IsNullOrWhiteSpace(result.AccessToken) || result.ExpiresAt <= DateTimeOffset.UtcNow)
        {
            throw new InvalidOperationException("The login endpoint returned an invalid token.");
        }

        if (string.IsNullOrWhiteSpace(result.UserId))
        {
            throw new InvalidOperationException("The login endpoint returned no user ID.");
        }

        await jsRuntime.InvokeVoidAsync("localStorage.setItem", TokenStorageKey, result.AccessToken);
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", EmailStorageKey, email);
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", ExpirationStorageKey, result.ExpiresAt.ToString("O"));
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", UserIdStorageKey, result.UserId);
        await jsRuntime.InvokeVoidAsync("localStorage.setItem", RoleStorageKey, result.Role ?? string.Empty);
        await WriteSessionVersionAsync();

        _accessToken = result.AccessToken;
        _email = email;
        _expiresAt = result.ExpiresAt;
        accessTokenStore.Set(_accessToken, _expiresAt);
        userSessionService.SetIdentity(result.UserId, result.Role);
        _initialized = true;
        NotifyAuthenticationStateChanged();
    }

    public async Task LogoutAsync()
    {
        try
        {
            await ClearStoredCredentialsAsync();
        }
        finally
        {
            _accessToken = null;
            _email = null;
            _expiresAt = null;
            accessTokenStore.Set(null, null);
            userSessionService.SetIdentity(null, null);
            _initialized = true;
            NotifyAuthenticationStateChanged();
        }
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        var message = string.IsNullOrWhiteSpace(body)
            ? $"Authentication request failed with status {(int)response.StatusCode}."
            : GetErrorMessage(body);

        throw new HttpRequestException(message, null, response.StatusCode);
    }

    private static string GetErrorMessage(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            var root = document.RootElement;

            if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Array)
            {
                var messages = errors.EnumerateArray()
                    .Where(error => error.ValueKind == JsonValueKind.String)
                    .Select(error => error.GetString())
                    .Where(message => !string.IsNullOrWhiteSpace(message));

                return string.Join(" ", messages);
            }

            if (root.TryGetProperty("message", out var messageElement)
                && messageElement.ValueKind == JsonValueKind.String)
            {
                return messageElement.GetString()!;
            }

            if (root.TryGetProperty("title", out var titleElement)
                && titleElement.ValueKind == JsonValueKind.String)
            {
                return titleElement.GetString()!;
            }
        }
        catch (JsonException)
        {
        }

        return body;
    }

    private async Task ClearStoredCredentialsAsync()
    {
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", TokenStorageKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", EmailStorageKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", ExpirationStorageKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", UserIdStorageKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", RoleStorageKey);
        await jsRuntime.InvokeVoidAsync("localStorage.removeItem", SessionVersionStorageKey);
        _accessToken = null;
        _email = null;
        _expiresAt = null;
        userSessionService.SetIdentity(null, null);
    }

    private async Task SubscribeToStorageChangesAsync()
    {
        _selfReference ??= DotNetObjectReference.Create(this);
        await jsRuntime.InvokeVoidAsync(
            "skillSnapAuthStorage.subscribe",
            _selfReference,
            SessionVersionStorageKey);
    }

    private async Task<StoredCredentials> ReadStoredCredentialsAsync()
    {
        return new StoredCredentials(
            await jsRuntime.InvokeAsync<string?>("localStorage.getItem", TokenStorageKey),
            await jsRuntime.InvokeAsync<string?>("localStorage.getItem", EmailStorageKey),
            await jsRuntime.InvokeAsync<string?>("localStorage.getItem", ExpirationStorageKey),
            await jsRuntime.InvokeAsync<string?>("localStorage.getItem", UserIdStorageKey),
            await jsRuntime.InvokeAsync<string?>("localStorage.getItem", RoleStorageKey));
    }

    private async Task ApplyStoredCredentialsAsync(StoredCredentials storedCredentials)
    {
        if (!IsValid(storedCredentials))
        {
            await ClearStoredCredentialsAsync();
            return;
        }

        _accessToken = storedCredentials.Token;
        _email = storedCredentials.Email;
        _expiresAt = DateTimeOffset.Parse(storedCredentials.Expiration!);
        accessTokenStore.Set(_accessToken, _expiresAt);
        userSessionService.SetIdentity(
            storedCredentials.UserId,
            string.IsNullOrWhiteSpace(storedCredentials.Role) ? null : storedCredentials.Role);
    }

    private static bool IsValid(StoredCredentials credentials)
    {
        return !string.IsNullOrWhiteSpace(credentials.Token)
            && DateTimeOffset.TryParse(credentials.Expiration, out var expiresAt)
            && expiresAt > DateTimeOffset.UtcNow
            && !string.IsNullOrWhiteSpace(credentials.UserId);
    }

    private async Task WriteSessionVersionAsync()
    {
        await jsRuntime.InvokeVoidAsync(
            "localStorage.setItem",
            SessionVersionStorageKey,
            Guid.NewGuid().ToString("N"));
    }

    private void NotifyAuthenticationStateChanged()
    {
        AuthenticationStateChanged?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_selfReference is null)
        {
            return;
        }

        await jsRuntime.InvokeVoidAsync("skillSnapAuthStorage.unsubscribe");
        _selfReference.Dispose();
        _initializationLock.Dispose();
    }

    private sealed record AuthRequest(string Email, string Password);

    private sealed record StoredCredentials(
        string? Token,
        string? Email,
        string? Expiration,
        string? UserId,
        string? Role);

    private sealed class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;

        public DateTimeOffset ExpiresAt { get; set; }

        public string UserId { get; set; } = string.Empty;

        public string? Role { get; set; }
    }
}
