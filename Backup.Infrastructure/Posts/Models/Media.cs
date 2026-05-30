namespace Backup.Infrastructure.Posts.Models;

public class PostMedia
{
    public required string Id { get; set; }
    public required string Url { get; set; }
    public required string Type { get; set; }
    public PostVideoInfo? VideoInfo { get; set; }

    public PostMedia Clone() =>
        new()
        {
            Id = Id,
            Url = Url,
            Type = Type,
            VideoInfo = VideoInfo?.Clone(),
        };
}

public class PostVideoInfo
{
    public int? DurationMilis { get; set; }
    public List<PostVariant>? Variants { get; set; }

    public PostVideoInfo Clone() =>
        new()
        {
            DurationMilis = DurationMilis,
            Variants = Variants?.Select(variant => variant.Clone()).ToList(),
        };
}

public class PostVariant
{
    public required string ContentType { get; set; }
    public int? Bitrate { get; set; }
    public required string Url { get; set; }

    public PostVariant Clone() =>
        new()
        {
            ContentType = ContentType,
            Bitrate = Bitrate,
            Url = Url,
        };
}
