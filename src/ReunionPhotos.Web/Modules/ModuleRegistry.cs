using ReunionPhotos.Web.Modules.OneDriveSync;

namespace ReunionPhotos.Web.Modules;

public static class ModuleRegistry
{
    // Add new modules here as you build them.
    public static readonly IReadOnlyList<IFeatureModule> All = new List<IFeatureModule>
    {
        new OneDriveSyncModule(),
    };

    public static void RegisterAll(IServiceCollection services, IConfiguration configuration)
    {
        foreach (var module in All)
        {
            module.RegisterServices(services, configuration);
        }
    }
}
