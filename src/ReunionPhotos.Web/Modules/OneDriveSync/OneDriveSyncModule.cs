namespace ReunionPhotos.Web.Modules.OneDriveSync;

public class OneDriveSyncModule : IFeatureModule
{
    public string Name => "OneDrive Sync";

    public void RegisterServices(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<GraphOptions>(configuration.GetSection(GraphOptions.SectionName));
        services.AddHttpClient<GraphApiClient>();
        services.AddHostedService<OneDriveSyncBackgroundService>();
    }
}
