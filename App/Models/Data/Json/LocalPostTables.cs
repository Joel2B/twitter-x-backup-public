namespace Backup.App.Models.Data.Json;

internal sealed class LocalPostTables
{
    public List<PostRow> Posts { get; set; } = [];
    public List<ProfileRow> Profiles { get; set; } = [];
    public List<HashtagRow> Hashtags { get; set; } = [];
    public List<MediaRow> Medias { get; set; } = [];
    public List<MediaVariantRow> MediaVariants { get; set; } = [];
    public List<IndexEntryRow> IndexEntries { get; set; } = [];
    public List<PostChangeRow> PostChanges { get; set; } = [];
    public List<PostChangeFieldRow> PostChangeFields { get; set; } = [];
}

internal sealed class PostRow
{
    public string Id { get; set; } = string.Empty;
    public string ProfileId { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool Retweeted { get; set; }
    public bool Favorited { get; set; }
    public bool Bookmarked { get; set; }
    public string CreatedAt { get; set; } = string.Empty;
    public bool Deleted { get; set; }
}

internal sealed class ProfileRow
{
    public string Id { get; set; } = string.Empty;
    public string? UserName { get; set; }
    public string? Name { get; set; }
    public string? BannerUrl { get; set; }
    public string? ImageUrl { get; set; }
    public bool? Following { get; set; }
    public int? CountMedia { get; set; }
}

internal sealed class HashtagRow
{
    public string PostId { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public int Ordinal { get; set; }
}

internal sealed class MediaRow
{
    public string PostId { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public string MediaId { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int? VideoDurationMilis { get; set; }
}

internal sealed class MediaVariantRow
{
    public string PostId { get; set; } = string.Empty;
    public int MediaOrdinal { get; set; }
    public int Ordinal { get; set; }
    public string ContentType { get; set; } = string.Empty;
    public int? Bitrate { get; set; }
    public string Url { get; set; } = string.Empty;
}

internal sealed class IndexEntryRow
{
    public string PostId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Origin { get; set; } = string.Empty;
    public string? Previous { get; set; }
    public string? Next { get; set; }
}

internal sealed class PostChangeRow
{
    public string PostId { get; set; } = string.Empty;
    public int Ordinal { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime Date { get; set; }
    public string ChangeType { get; set; } = string.Empty;
}

internal sealed class PostChangeFieldRow
{
    public string PostId { get; set; } = string.Empty;
    public int ChangeOrdinal { get; set; }
    public int Ordinal { get; set; }
    public string Field { get; set; } = string.Empty;
    public string? OldValueJson { get; set; }
    public string? NewValueJson { get; set; }
}
