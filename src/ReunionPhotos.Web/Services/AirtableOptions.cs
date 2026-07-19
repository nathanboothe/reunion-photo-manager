namespace ReunionPhotos.Web.Services;

public class AirtableOptions
{
    public const string SectionName = "Airtable";

    public string ApiKey { get; set; } = "";
    public string BaseId { get; set; } = "";

    // Table names - change these if you name your Airtable tables differently.
    public string AlbumsTable { get; set; } = "Albums";
    public string PhotosTable { get; set; } = "Photos";
    public string EntriesTable { get; set; } = "Entries";
    public string FamilyMembersTable { get; set; } = "FamilyMembers";

    // Single-row-per-key table used to persist things that change at
    // runtime, like the current OneDrive refresh token (see GraphApiClient).
    // Fields: Key (single line text), Value (long text).
    public string ConfigTable { get; set; } = "AppConfig";
}
