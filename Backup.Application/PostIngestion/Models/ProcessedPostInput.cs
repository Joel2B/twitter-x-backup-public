namespace Backup.Application.PostIngestion.Models;

public class ProcessedPostInput
{
    public string Id { get; set; } = string.Empty;
    public ProcessedPostProfileInput Profile { get; set; } = new();
    public string Description { get; set; } = string.Empty;
    public bool? Retweeted { get; set; }
    public bool? Favorited { get; set; }
    public bool? Bookmarked { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public List<string>? Hashtags { get; set; }
    public List<ProcessedPostMediaInput>? Medias { get; set; }
    public bool? Deleted { get; set; }
}

public class ProcessedPostProfileInput
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Name { get; set; }
    public string? BannerUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool? Following { get; set; }
}

public class ProcessedPostMediaInput
{
    public string Id { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public ProcessedPostVideoInfoInput? VideoInfo { get; set; }
}

public class ProcessedPostVideoInfoInput
{
    public int? DurationMilis { get; set; }
    public List<ProcessedPostVariantInput>? Variants { get; set; }
}

public class ProcessedPostVariantInput
{
    public string ContentType { get; set; } = string.Empty;
    public int? Bitrate { get; set; }
    public string Url { get; set; } = string.Empty;
}
