namespace Backup.Domain.Posts;

public class MediaInput
{
    public required string Id { get; set; }
    public required PostProfile Profile { get; set; }
    public List<PostMedia>? Medias { get; set; }
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
