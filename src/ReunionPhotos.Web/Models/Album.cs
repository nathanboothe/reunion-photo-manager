namespace ReunionPhotos.Web.Models;

// One row per OneDrive folder you want the app to show as an album.
// Maps 1:1 to a record in the "Albums" Airtable table.
public class Album
{
    // Airtable record id, e.g. "recXXXXXXXXXXXXXX"
    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    // The OneDrive drive that owns this folder (from Graph API)
    public string DriveId { get; set; } = "";

    // The OneDrive folder's item id (Graph API), used to list its children
    public string OneDriveFolderId { get; set; } = "";

    public bool Active { get; set; } = true;
}
