namespace ReunionPhotos.Web.Modules.OneDriveSync;

public class GraphOptions
{
    public const string SectionName = "Graph";

    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";

    // Your organization's Azure AD tenant id (a GUID) or domain name
    // (e.g. "contoso.onmicrosoft.com"). Found on the Azure/Entra portal's
    // app registration overview page as "Directory (tenant) ID".
    public string TenantId { get; set; } = "";

    // Obtained once via the OAuth authorization-code flow (see README) and
    // stored as a Render environment variable from then on. offline_access
    // at consent time is what makes this refresh token keep working long
    // term without you having to log in again.
    public string RefreshToken { get; set; } = "";

    // How often the background service checks OneDrive for new photos.
    public int SyncIntervalMinutes { get; set; } = 60;
}
