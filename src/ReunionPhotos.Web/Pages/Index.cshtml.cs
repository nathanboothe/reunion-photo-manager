using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReunionPhotos.Web.Models;
using ReunionPhotos.Web.Services;

namespace ReunionPhotos.Web.Pages;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AirtableService _airtable;

    public IndexModel(AirtableService airtable)
    {
        _airtable = airtable;
    }

    public List<Album> Albums { get; private set; } = new();
    public Dictionary<string, List<Photo>> PhotosByAlbum { get; private set; } = new();

    public async Task OnGetAsync(CancellationToken ct)
    {
        Albums = await _airtable.GetActiveAlbumsAsync(ct);

        foreach (var album in Albums)
        {
            PhotosByAlbum[album.Id] = await _airtable.GetPhotosByAlbumAsync(album.Id, ct);
        }
    }
}
