using Microsoft.Extensions.Options;
using ReunionPhotos.Web.Models;
using ReunionPhotos.Web.Services;

namespace ReunionPhotos.Web.Modules.OneDriveSync;

// Runs for the lifetime of the app. On an interval, it walks every active
// Album, lists what's currently in the matching OneDrive folder, and
// upserts photo metadata into Airtable by OneDriveItemId. It never touches
// or downloads the actual image bytes - that happens on demand when a
// family member views a photo (see the /image/{photoId} endpoint).
public class OneDriveSyncBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly GraphOptions _options;
    private readonly ILogger<OneDriveSyncBackgroundService> _logger;

    public OneDriveSyncBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<GraphOptions> options,
        ILogger<OneDriveSyncBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Small initial delay so the first sync doesn't compete with app startup.
        await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "OneDrive sync run failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(_options.SyncIntervalMinutes), stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var airtable = scope.ServiceProvider.GetRequiredService<AirtableService>();
        var graph = scope.ServiceProvider.GetRequiredService<GraphApiClient>();

        var albums = await airtable.GetActiveAlbumsAsync(ct);
        _logger.LogInformation("Starting OneDrive sync for {Count} active album(s)", albums.Count);

        foreach (var album in albums)
        {
            var items = await graph.ListFolderItemsAsync(album.DriveId, album.OneDriveFolderId, ct);

            foreach (var item in items)
            {
                await airtable.UpsertPhotoAsync(new Photo
                {
                    AlbumId = album.Id,
                    DriveId = album.DriveId,
                    OneDriveItemId = item.Id,
                    FileName = item.Name,
                    DateTaken = item.CreatedDateTime,
                }, ct);
            }

            _logger.LogInformation("Synced {Count} photo(s) for album '{Album}'", items.Count, album.Name);
        }
    }
}
