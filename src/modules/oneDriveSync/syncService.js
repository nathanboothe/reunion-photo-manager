// Runs for the lifetime of the app. On an interval, it walks every active
// Album, lists what's currently in the matching OneDrive folder, and
// upserts photo metadata into Airtable by oneDriveItemId. It never touches
// or downloads the actual image bytes - that happens on demand when a
// family member views a photo (see routes/image.js).
const config = require('../../config');
const airtable = require('../../services/airtableService');
const graph = require('./graphApiClient');

async function runOnce() {
  const albums = await airtable.getActiveAlbums();
  console.log(`Starting OneDrive sync for ${albums.length} active album(s)`);

  for (const album of albums) {
    const items = await graph.listFolderItems(album.driveId, album.oneDriveFolderId);

    for (const item of items) {
      await airtable.upsertPhoto({
        albumId: album.id,
        driveId: album.driveId,
        oneDriveItemId: item.id,
        fileName: item.name,
        dateTaken: item.createdDateTime || null,
      });
    }

    console.log(`Synced ${items.length} photo(s) for album '${album.name}'`);
  }
}

function start() {
  // Small initial delay so the first sync doesn't compete with app startup.
  setTimeout(async function loop() {
    try {
      await runOnce();
    } catch (err) {
      console.error('OneDrive sync run failed:', err);
    }
    setTimeout(loop, config.graph.syncIntervalMinutes * 60 * 1000);
  }, 10 * 1000);
}

module.exports = { start, runOnce };
