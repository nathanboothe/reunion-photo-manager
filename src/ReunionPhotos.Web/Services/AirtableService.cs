using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using ReunionPhotos.Web.Models;

namespace ReunionPhotos.Web.Services;

// Thin wrapper around the Airtable REST API. Every other service in the app
// (gallery pages, comment form, sync background service) goes through this
// class rather than calling HttpClient directly, so the Airtable field
// names only need to be known in one place.
public class AirtableService
{
    private readonly HttpClient _http;
    private readonly AirtableOptions _options;

    public AirtableService(HttpClient http, IOptions<AirtableOptions> options)
    {
        _options = options.Value;
        http.BaseAddress = new Uri($"https://api.airtable.com/v0/{_options.BaseId}/");
        http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        _http = http;
    }

    // ---------- Albums ----------

    public async Task<List<Album>> GetActiveAlbumsAsync(CancellationToken ct = default)
    {
        var records = await ListRecordsAsync(_options.AlbumsTable, "{Active} = TRUE()", ct);
        return records.Select(r => new Album
        {
            Id = r.Id,
            Name = GetString(r.Fields, "Name"),
            DriveId = GetString(r.Fields, "DriveId"),
            OneDriveFolderId = GetString(r.Fields, "OneDriveFolderId"),
            Active = GetBool(r.Fields, "Active"),
        }).ToList();
    }

    // ---------- Photos ----------

    public async Task<List<Photo>> GetPhotosByAlbumAsync(string albumId, CancellationToken ct = default)
    {
        // Airtable linked-record fields store an array of record ids; the
        // formula below checks whether albumId is inside that array.
        var formula = $"FIND('{albumId}', ARRAYJOIN({{Album}})) > 0";
        var records = await ListRecordsAsync(_options.PhotosTable, formula, ct);
        return records.Select(MapPhoto).ToList();
    }

    public async Task<Photo?> GetPhotoByIdAsync(string photoId, CancellationToken ct = default)
    {
        var record = await GetRecordAsync(_options.PhotosTable, photoId, ct);
        return record is null ? null : MapPhoto(record);
    }

    public async Task<Photo?> FindPhotoByOneDriveItemIdAsync(string oneDriveItemId, CancellationToken ct = default)
    {
        var formula = $"{{OneDriveItemId}} = '{oneDriveItemId}'";
        var records = await ListRecordsAsync(_options.PhotosTable, formula, ct);
        return records.Count == 0 ? null : MapPhoto(records[0]);
    }

    // Used by the OneDrive sync background service: creates the photo record
    // if it hasn't been seen before, or just refreshes LastSynced if it has.
    public async Task UpsertPhotoAsync(Photo photo, CancellationToken ct = default)
    {
        var existing = await FindPhotoByOneDriveItemIdAsync(photo.OneDriveItemId, ct);
        var fields = new Dictionary<string, object?>
        {
            ["Album"] = new[] { photo.AlbumId },
            ["DriveId"] = photo.DriveId,
            ["OneDriveItemId"] = photo.OneDriveItemId,
            ["FileName"] = photo.FileName,
            ["DateTaken"] = photo.DateTaken?.ToString("O"),
            ["LastSynced"] = DateTimeOffset.UtcNow.ToString("O"),
        };

        if (existing is null)
        {
            await CreateRecordAsync(_options.PhotosTable, fields, ct);
        }
        else
        {
            await UpdateRecordAsync(_options.PhotosTable, existing.Id, fields, ct);
        }
    }

    private static Photo MapPhoto(AirtableRecord r) => new()
    {
        Id = r.Id,
        AlbumId = GetLinkedId(r.Fields, "Album"),
        DriveId = GetString(r.Fields, "DriveId"),
        OneDriveItemId = GetString(r.Fields, "OneDriveItemId"),
        FileName = GetString(r.Fields, "FileName"),
        DateTaken = GetDate(r.Fields, "DateTaken"),
        LastSynced = GetDate(r.Fields, "LastSynced") ?? DateTimeOffset.MinValue,
    };

    // ---------- Entries (comments / name tags / stories) ----------

    public async Task<List<Entry>> GetEntriesForPhotoAsync(string photoId, CancellationToken ct = default)
    {
        var formula = $"FIND('{photoId}', ARRAYJOIN({{Photo}})) > 0";
        var records = await ListRecordsAsync(_options.EntriesTable, formula, ct);
        return records
            .Select(r => new Entry
            {
                Id = r.Id,
                PhotoId = GetLinkedId(r.Fields, "Photo"),
                FamilyMemberId = GetLinkedId(r.Fields, "FamilyMember"),
                FamilyMemberName = GetString(r.Fields, "FamilyMemberName"),
                Type = GetString(r.Fields, "Type") == "Story" ? EntryType.Story : EntryType.NameTag,
                Text = GetString(r.Fields, "Text"),
                CreatedAt = GetDate(r.Fields, "CreatedAt") ?? DateTimeOffset.MinValue,
            })
            .OrderBy(e => e.CreatedAt)
            .ToList();
    }

    public async Task AddEntryAsync(Entry entry, CancellationToken ct = default)
    {
        var fields = new Dictionary<string, object?>
        {
            ["Photo"] = new[] { entry.PhotoId },
            ["FamilyMember"] = new[] { entry.FamilyMemberId },
            ["FamilyMemberName"] = entry.FamilyMemberName,
            ["Type"] = entry.Type == EntryType.Story ? "Story" : "Name tag",
            ["Text"] = entry.Text,
            ["CreatedAt"] = DateTimeOffset.UtcNow.ToString("O"),
        };
        await CreateRecordAsync(_options.EntriesTable, fields, ct);
    }

    // ---------- Family members ----------

    public async Task<List<FamilyMember>> GetActiveFamilyMembersAsync(CancellationToken ct = default)
    {
        var records = await ListRecordsAsync(_options.FamilyMembersTable, "{Active} = TRUE()", ct);
        return records.Select(r => new FamilyMember
        {
            Id = r.Id,
            Name = GetString(r.Fields, "Name"),
            PinHash = GetString(r.Fields, "PinHash"),
            Active = GetBool(r.Fields, "Active"),
        }).ToList();
    }

    // ---------- Runtime config (e.g. the rotating OneDrive refresh token) ----------

    public async Task<string?> GetConfigValueAsync(string key, CancellationToken ct = default)
    {
        var formula = $"{{Key}} = '{key}'";
        var records = await ListRecordsAsync(_options.ConfigTable, formula, ct);
        return records.Count == 0 ? null : GetString(records[0].Fields, "Value");
    }

    public async Task SetConfigValueAsync(string key, string value, CancellationToken ct = default)
    {
        var formula = $"{{Key}} = '{key}'";
        var records = await ListRecordsAsync(_options.ConfigTable, formula, ct);
        var fields = new Dictionary<string, object?> { ["Key"] = key, ["Value"] = value };

        if (records.Count == 0)
            await CreateRecordAsync(_options.ConfigTable, fields, ct);
        else
            await UpdateRecordAsync(_options.ConfigTable, records[0].Id, fields, ct);
    }

    // ---------- Low-level Airtable REST helpers ----------

    private async Task<List<AirtableRecord>> ListRecordsAsync(string table, string? filterFormula, CancellationToken ct)
    {
        var all = new List<AirtableRecord>();
        string? offset = null;

        do
        {
            var url = $"{Uri.EscapeDataString(table)}?pageSize=100";
            if (!string.IsNullOrEmpty(filterFormula))
                url += $"&filterByFormula={Uri.EscapeDataString(filterFormula)}";
            if (!string.IsNullOrEmpty(offset))
                url += $"&offset={offset}";

            using var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<AirtableListResponse>(cancellationToken: ct);

            if (payload?.Records is not null)
                all.AddRange(payload.Records);
            offset = payload?.Offset;
        }
        while (!string.IsNullOrEmpty(offset));

        return all;
    }

    private async Task<AirtableRecord?> GetRecordAsync(string table, string recordId, CancellationToken ct)
    {
        using var response = await _http.GetAsync($"{Uri.EscapeDataString(table)}/{recordId}", ct);
        if (!response.IsSuccessStatusCode) return null;
        return await response.Content.ReadFromJsonAsync<AirtableRecord>(cancellationToken: ct);
    }

    private async Task CreateRecordAsync(string table, Dictionary<string, object?> fields, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { fields });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http.PostAsync(Uri.EscapeDataString(table), content, ct);
        response.EnsureSuccessStatusCode();
    }

    private async Task UpdateRecordAsync(string table, string recordId, Dictionary<string, object?> fields, CancellationToken ct)
    {
        var body = JsonSerializer.Serialize(new { fields });
        using var content = new StringContent(body, Encoding.UTF8, "application/json");
        using var response = await _http.PatchAsync($"{Uri.EscapeDataString(table)}/{recordId}", content, ct);
        response.EnsureSuccessStatusCode();
    }

    // ---------- Field parsing helpers ----------

    private static string GetString(Dictionary<string, JsonElement> fields, string key) =>
        fields.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

    private static bool GetBool(Dictionary<string, JsonElement> fields, string key) =>
        fields.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.True;

    private static DateTimeOffset? GetDate(Dictionary<string, JsonElement> fields, string key)
    {
        var s = GetString(fields, key);
        return DateTimeOffset.TryParse(s, out var d) ? d : null;
    }

    // Linked-record fields come back as a JSON array of record ids; we only
    // ever link to a single parent record, so take the first one.
    private static string GetLinkedId(Dictionary<string, JsonElement> fields, string key)
    {
        if (fields.TryGetValue(key, out var v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0)
            return v[0].GetString() ?? "";
        return "";
    }

    private class AirtableListResponse
    {
        public List<AirtableRecord> Records { get; set; } = new();
        public string? Offset { get; set; }
    }
}

public class AirtableRecord
{
    public string Id { get; set; } = "";
    public Dictionary<string, JsonElement> Fields { get; set; } = new();
}
