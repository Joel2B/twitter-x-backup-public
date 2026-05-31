namespace Backup.Application.Media.Models;

public sealed class MediaDownloadProjectionConfig
{
    public required MediaDownloadProjectionRuleConfig Banner { get; init; }
    public required MediaDownloadProjectionRuleConfig Profile { get; init; }
    public required MediaDownloadProjectionRuleConfig Photo { get; init; }
    public required MediaDownloadProjectionVariantConfig Video { get; init; }
    public required MediaDownloadProjectionVariantConfig Gif { get; init; }
}

public sealed class MediaDownloadProjectionRuleConfig
{
    public List<string>? Filters { get; init; }
    public List<string>? Types { get; init; }
    public List<string>? Dimensions { get; init; }
    public List<string>? Sizes { get; init; }
}

public sealed class MediaDownloadProjectionVariantConfig
{
    public required MediaDownloadProjectionRuleConfig Thumb { get; init; }
    public List<string>? Types { get; init; }
}
