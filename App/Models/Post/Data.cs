namespace Backup.App.Models.Post;

public class Data
{
    public required string Id { get; set; }
    public required Profile Profile { get; set; }
    public required string Description { get; set; }
    public required bool Retweeted { get; set; }
    public required bool Favorited { get; set; }
    public required bool Bookmarked { get; set; }
    public required string CreatedAt { get; set; }
    public List<string>? Hashtags { get; set; }
    public List<Media>? Medias { get; set; }
    public bool Deleted { get; set; } = false;

    public Data Clone() =>
        new()
        {
            Id = Id,
            Profile = Profile.Clone(),
            Description = Description,
            Retweeted = Retweeted,
            Favorited = Favorited,
            Bookmarked = Bookmarked,
            CreatedAt = CreatedAt,
            Hashtags = Hashtags is null ? null : [.. Hashtags],
            Medias = Medias?.Select(media => media.Clone()).ToList(),
            Deleted = Deleted,
        };
}
