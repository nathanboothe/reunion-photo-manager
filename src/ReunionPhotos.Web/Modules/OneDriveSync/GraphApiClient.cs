using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ReunionPhotos.Web.Services;

namespace ReunionPhotos.Web.Modules.OneDriveSync;

// Talks to Microsoft Graph for the personal OneDrive account. Handles the
// token refresh dance, including the fact that personal-account refresh
// tokens rotate on every use - each refresh call returns a brand new
// refresh token, and the old one stops working. We persist the latest one
// in Airtable so this survives app restarts and redeploys.
public class GraphApiClient
{
    private const string GraphBase = "https://graph.microsoft.com/v1.0";
    private const string ConfigKeyRefreshToken = "OneDriveRefreshToken";

    private readonly HttpClient _http;
    private readonly GraphOptions _options;
    private readonly AirtableService _airtable;
    private readonly ILogger<GraphApiClient> _logger;

    private string? _accessToken;
    private DateTimeOffset _accessTokenExpiresAt = DateTimeOffset.MinValue;

    public GraphApiClient(HttpClient http, IOptions<GraphOptions> options, AirtableService airtable, ILogger<GraphApiClient> logger)
    {
        _http = http;
        _options = options.Value;
        _airtable = airtable;
        _logger = logger;
    }

    public async Task<List<DriveItemDto>> ListFolderItemsAsync(string driveId, string folderId, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        var url = $"{GraphBase}/drives/{driveId}/items/{folderId}/children" +
                  "?$select=id,name,file,photo,image,createdDateTime";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        using var response = await _http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DriveChildrenResponse>(cancellationToken: ct);
        return (payload?.Value ?? new())
            .Where(i => i.File is not null) // skip subfolders
            .ToList();
    }

    // Streams the actual image bytes for a photo. Used by the /image/{photoId}
    // proxy endpoint so the browser never needs a direct (short-lived) OneDrive
    // download URL - it always asks our server, which fetches fresh each time.
    public async Task<(Stream Content, string ContentType)> GetImageStreamAsync(
        string driveId, string itemId, bool thumbnail, CancellationToken ct = default)
    {
        var token = await GetAccessTokenAsync(ct);
        var url = thumbnail
            ? $"{GraphBase}/drives/{driveId}/items/{itemId}/thumbnails/0/large/content"
            : $"{GraphBase}/drives/{driveId}/items/{itemId}/content";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var contentType = response.Content.Headers.ContentType?.MediaType ?? "image/jpeg";
        var stream = await response.Content.ReadAsStreamAsync(ct);
        return (stream, contentType);
    }

    private async Task<string> GetAccessTokenAsync(CancellationToken ct)
    {
        if (_accessToken is not null && DateTimeOffset.UtcNow < _accessTokenExpiresAt)
            return _accessToken;

        var tokenEndpoint = $"https://login.microsoftonline.com/{_options.TenantId}/oauth2/v2.0/token";

        // Prefer the most recently persisted refresh token (it may have
        // rotated since the app started); fall back to the configured seed
        // value the very first time the app runs.
        var refreshToken = await _airtable.GetConfigValueAsync(ConfigKeyRefreshToken, ct) ?? _options.RefreshToken;

        var form = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["client_secret"] = _options.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["scope"] = "Files.Read offline_access",
        };

        using var response = await _http.PostAsync(tokenEndpoint, new FormUrlEncodedContent(form), ct);
        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("OneDrive token refresh failed: {Body}", body);
            throw new InvalidOperationException("Failed to refresh Microsoft Graph access token. See logs for details.");
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        _accessToken = root.GetProperty("access_token").GetString();
        var expiresIn = root.GetProperty("expires_in").GetInt32();
        _accessTokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60); // refresh a minute early

        // Persist the new refresh token - personal Microsoft accounts issue
        // a new one on every refresh and invalidate the old one.
        if (root.TryGetProperty("refresh_token", out var newRefreshTokenEl))
        {
            var newRefreshToken = newRefreshTokenEl.GetString();
            if (!string.IsNullOrEmpty(newRefreshToken))
                await _airtable.SetConfigValueAsync(ConfigKeyRefreshToken, newRefreshToken, ct);
        }

        return _accessToken!;
    }

    private class DriveChildrenResponse
    {
        public List<DriveItemDto> Value { get; set; } = new();
    }
}

public class DriveItemDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public object? File { get; set; }
    public DateTimeOffset? CreatedDateTime { get; set; }
}
