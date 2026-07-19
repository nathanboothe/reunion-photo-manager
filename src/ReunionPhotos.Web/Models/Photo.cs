namespace ReunionPhotos.Web.Models;

// One row per photo. Maps 1:1 to a record in the "Photos" Airtable table.
//
// OneDriveItemId is the important field: it's an immutable id assigned by
// OneDrive that never changes even if the file is renamed or moved within
// the same drive. That's what gives every comment thread a permanent link
// to "this exact photo", regardless of what your family calls the file.
public class Photo
{
    public string Id { get; set; } = "";

    public string AlbumId { get; set; } = "";

    public string DriveId { get; set; } = "";

    public string OneDriveItemId { get; set; } = "";

    public string FileName { get; set; } = "";

    public DateTimeOffset? DateTaken { get; set; }

    public DateTimeOffset LastSynced { get; set; }
}
