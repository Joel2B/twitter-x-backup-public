namespace Backup.Application.Posts.Models;

public sealed class PostTableMaterializationInput
{
    public IReadOnlyList<PostTablePostRow> Posts { get; init; } = [];
    public IReadOnlyList<PostTableProfileRow> Profiles { get; init; } = [];
    public IReadOnlyList<PostTableHashtagRow> Hashtags { get; init; } = [];
    public IReadOnlyList<PostTableMediaRow> Medias { get; init; } = [];
    public IReadOnlyList<PostTableMediaVariantRow> MediaVariants { get; init; } = [];
    public IReadOnlyList<PostTableIndexEntryRow> IndexEntries { get; init; } = [];
    public IReadOnlyList<PostTableMetaRow> PostMeta { get; init; } = [];
}

public sealed class PostTablePostRow
{
    public required string Id { get; init; }
    public required string ProfileId { get; init; }
    public required string Description { get; init; }
    public bool Retweeted { get; init; }
    public bool Favorited { get; init; }
    public bool Bookmarked { get; init; }
    public required string CreatedAt { get; init; }
}

public sealed class PostTableProfileRow
{
    public required string Id { get; init; }
    public string? UserName { get; init; }
    public string? Name { get; init; }
    public string? BannerUrl { get; init; }
    public string? ImageUrl { get; init; }
    public bool? Following { get; init; }
    public int? CountMedia { get; init; }
}

public sealed class PostTableHashtagRow
{
    public required string PostId { get; init; }
    public required string Value { get; init; }
    public int Ordinal { get; init; }
}

public sealed class PostTableMediaRow
{
    public required string PostId { get; init; }
    public int Ordinal { get; init; }
    public required string MediaId { get; init; }
    public required string Url { get; init; }
    public required string Type { get; init; }
    public int? VideoDurationMilis { get; init; }
}

public sealed class PostTableMediaVariantRow
{
    public required string PostId { get; init; }
    public int MediaOrdinal { get; init; }
    public int Ordinal { get; init; }
    public required string ContentType { get; init; }
    public int? Bitrate { get; init; }
    public required string Url { get; init; }
}

public sealed class PostTableIndexEntryRow
{
    public required string PostId { get; init; }
    public required string UserId { get; init; }
    public required string Origin { get; init; }
    public string? Previous { get; init; }
    public string? Next { get; init; }
}

public sealed class PostTableMetaRow
{
    public required string Id { get; init; }
    public bool Deleted { get; init; }
}
