namespace ReunionPhotos.Web.Modules;

// Every optional feature (OneDrive sync today; things like reactions,
// a slideshow mode, or a "download original" button later) implements this
// interface. Program.cs discovers modules through ModuleRegistry.All and
// calls RegisterServices on each one at startup.
//
// To add a new feature later:
//   1. Create a new folder under Modules/ (e.g. Modules/Reactions).
//   2. Add a class that implements IFeatureModule.
//   3. Add one line to ModuleRegistry.All below.
// Nothing else in the app needs to change.
public interface IFeatureModule
{
    string Name { get; }

    void RegisterServices(IServiceCollection services, IConfiguration configuration);
}
