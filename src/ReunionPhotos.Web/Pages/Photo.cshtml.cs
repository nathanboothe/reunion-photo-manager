using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using ReunionPhotos.Web.Models;
using ReunionPhotos.Web.Services;

namespace ReunionPhotos.Web.Pages;

[Authorize]
public class PhotoModel : PageModel
{
    private readonly AirtableService _airtable;

    public PhotoModel(AirtableService airtable)
    {
        _airtable = airtable;
    }

    public Models.Photo? Photo { get; private set; }
    public List<Entry> Entries { get; private set; } = new();

    [BindProperty]
    public EntryType Type { get; set; }

    [BindProperty]
    public string Text { get; set; } = "";

    public async Task OnGetAsync(string photoId, CancellationToken ct)
    {
        Photo = await _airtable.GetPhotoByIdAsync(photoId, ct);
        if (Photo is not null)
        {
            Entries = await _airtable.GetEntriesForPhotoAsync(Photo.Id, ct);
        }
    }

    public async Task<IActionResult> OnPostAsync(string photoId, CancellationToken ct)
    {
        Photo = await _airtable.GetPhotoByIdAsync(photoId, ct);
        if (Photo is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(Text))
        {
            var memberId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
            var memberName = User.Identity?.Name ?? "";

            await _airtable.AddEntryAsync(new Entry
            {
                PhotoId = Photo.Id,
                FamilyMemberId = memberId,
                FamilyMemberName = memberName,
                Type = Type,
                Text = Text.Trim(),
            }, ct);
        }

        return RedirectToPage("/Photo", new { photoId });
    }
}
