namespace Backup.App.Models.Post;

public class Media
{
    public required string Id { get; set; }
    public required string Url { get; set; }
    public required string Type { get; set; }
    public VideoInfo? VideoInfo { get; set; }
}

public class VideoInfo
{
    public int? DurationMilis { get; set; }
    public List<Variant>? Variants { get; set; }
}

public class Variant
{
    public required string ContentType { get; set; }
    public int? Bitrate { get; set; }
    public required string Url { get; set; }
}
