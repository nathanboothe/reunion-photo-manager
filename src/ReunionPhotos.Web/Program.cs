using Microsoft.AspNetCore.Authentication.Cookies;
using ReunionPhotos.Web.Modules;
using ReunionPhotos.Web.Modules.OneDriveSync;
using ReunionPhotos.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Core configuration ---
builder.Services.Configure<AirtableOptions>(builder.Configuration.GetSection(AirtableOptions.SectionName));
builder.Services.AddHttpClient<AirtableService>();
builder.Services.AddScoped<PinAuthService>();

// --- PIN-based cookie authentication ---
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/Login";
        options.ExpireTimeSpan = TimeSpan.FromDays(30);
        options.SlidingExpiration = true;
        options.Cookie.Name = "ReunionPhotosAuth";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
builder.Services.AddAuthorization();

builder.Services.AddRazorPages();

// --- Feature modules (OneDrive sync today; add more in Modules/) ---
ModuleRegistry.RegisterAll(builder.Services, builder.Configuration);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();

// Streams photo bytes from OneDrive on demand rather than caching Graph
// API's short-lived download URLs - this endpoint always fetches fresh,
// so links in the gallery never go stale.
app.MapGet("/image/{photoId}", async (
        string photoId,
        bool thumb,
        AirtableService airtable,
        GraphApiClient graph,
        CancellationToken ct) =>
    {
        var photo = await airtable.GetPhotoByIdAsync(photoId, ct);
        if (photo is null) return Results.NotFound();

        var (stream, contentType) = await graph.GetImageStreamAsync(photo.DriveId, photo.OneDriveItemId, thumb, ct);
        return Results.File(stream, contentType);
    })
    .RequireAuthorization();

app.Run();
