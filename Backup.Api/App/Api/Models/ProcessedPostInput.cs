using System.ComponentModel.DataAnnotations;

namespace Backup.Api.Models;

public class ProcessedPostInput
{
    [Required]
    [RegularExpression(@".*\S.*", ErrorMessage = "id is required.")]
    public string Id { get; set; } = string.Empty;

    [Required]
    public ProcessedPostProfileInput Profile { get; set; } = new();

    [Required]
    [RegularExpression(@"(?s).*\S.*", ErrorMessage = "description is required.")]
    public string Description { get; set; } = string.Empty;

    [Required]
    public bool? Retweeted { get; set; }

    [Required]
    public bool? Favorited { get; set; }

    [Required]
    public bool? Bookmarked { get; set; }

    [Required]
    [RegularExpression(@".*\S.*", ErrorMessage = "createdAt is required.")]
    public string CreatedAt { get; set; } = string.Empty;

    public List<string>? Hashtags { get; set; }
    public List<ProcessedPostMediaInput>? Medias { get; set; }
    public bool? Deleted { get; set; }
}

public class ProcessedPostProfileInput
{
    [Required]
    [RegularExpression(@".*\S.*", ErrorMessage = "profile.id is required.")]
    public string Id { get; set; } = string.Empty;

    public string? UserName { get; set; }
    public string? Name { get; set; }
    public string? BannerUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool? Following { get; set; }
}

public class ProcessedPostMediaInput
{
    [Required]
    [RegularExpression(@".*\S.*", ErrorMessage = "media.id is required.")]
    public string Id { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@".*\S.*", ErrorMessage = "media.url is required.")]
    public string Url { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@".*\S.*", ErrorMessage = "media.type is required.")]
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
    [Required]
    [RegularExpression(@".*\S.*", ErrorMessage = "variant.contentType is required.")]
    public string ContentType { get; set; } = string.Empty;

    public int? Bitrate { get; set; }

    [Required]
    [RegularExpression(@".*\S.*", ErrorMessage = "variant.url is required.")]
    public string Url { get; set; } = string.Empty;
}

