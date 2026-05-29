namespace Backup.Domain.Posts;

public class PostData
{
    public required string Id { get; set; }
    public required PostProfile Profile { get; set; }
    public required string Description { get; set; }
    public required bool Retweeted { get; set; }
    public required bool Favorited { get; set; }
    public required bool Bookmarked { get; set; }
    public required string CreatedAt { get; set; }
    public List<string>? Hashtags { get; set; }
    public List<PostMedia>? Medias { get; set; }
    public bool Deleted { get; set; } = false;

    public PostData Clone() =>
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

