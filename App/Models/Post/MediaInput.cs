namespace Backup.App.Models.Post;

public class MediaInput
{
    public required string Id { get; set; }
    public required Profile Profile { get; set; }
    public List<Media>? Medias { get; set; }
    public bool Deleted { get; set; }

    public MediaInput Clone() =>
        new()
        {
            Id = Id,
            Profile = Profile.Clone(),
            Medias = Medias?.Select(media => media.Clone()).ToList(),
            Deleted = Deleted,
        };
}
