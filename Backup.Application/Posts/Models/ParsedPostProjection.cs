namespace Backup.Application.Posts.Models;

public class ParsedPostProjection
{
    public required string Id { get; set; }
    public required ParsedPostProfileProjection Profile { get; set; }
    public required string Description { get; set; }
    public required bool Retweeted { get; set; }
    public required bool Favorited { get; set; }
    public required bool Bookmarked { get; set; }
    public required string CreatedAt { get; set; }
    public List<string>? Hashtags { get; set; }
    public List<ParsedPostMediaProjection>? Medias { get; set; }
}

public class ParsedPostProfileProjection
{
    public required string Id { get; set; }
    public string? UserName { get; set; }
    public string? Name { get; set; }
    public string? BannerUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool? Following { get; set; }
    public int? MediaCount { get; set; }
}

public class ParsedPostMediaProjection
{
    public required string Id { get; set; }
    public required string Url { get; set; }
    public required string Type { get; set; }
    public ParsedPostVideoInfoProjection? VideoInfo { get; set; }
}

public class ParsedPostVideoInfoProjection
{
    public int? DurationMilis { get; set; }
    public List<ParsedPostVariantProjection>? Variants { get; set; }
}

public class ParsedPostVariantProjection
{
    public required string ContentType { get; set; }
    public int? Bitrate { get; set; }
    public required string Url { get; set; }
}
