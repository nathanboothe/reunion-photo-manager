namespace ReunionPhotos.Web.Models;

public enum EntryType
{
    NameTag,
    Story
}

// One row per comment/tag/story. Maps 1:1 to a record in the "Entries"
// Airtable table, linked to both a Photo and a FamilyMember. This link is
// what lets you reliably answer "who said what about this photo" later,
// since FamilyMemberId comes from an authenticated PIN session rather than
// a typed name.
public class Entry
{
    public string Id { get; set; } = "";

    public string PhotoId { get; set; } = "";

    public string FamilyMemberId { get; set; } = "";

    // Denormalized for display so pages don't need a second lookup just to
    // show "- Grandma Jo" under a comment.
    public string FamilyMemberName { get; set; } = "";

    public EntryType Type { get; set; }

    public string Text { get; set; } = "";

    public DateTimeOffset CreatedAt { get; set; }
}
