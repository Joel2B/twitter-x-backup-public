namespace Backup.App.Data.Posts;

public partial class SqlitePostData
{
    private sealed class PostProfileEntity
    {
        public required string Id { get; set; }
        public string? UserName { get; set; }
        public string? Name { get; set; }
        public string? BannerUrl { get; set; }
        public string? ImageUrl { get; set; }
        public bool? Following { get; set; }
        public int? CountMedia { get; set; }

        public List<PostEntity> Posts { get; set; } = [];
    }

    private sealed class PostEntity
    {
        public required string Id { get; set; }
        public required string ProfileId { get; set; }
        public PostProfileEntity Profile { get; set; } = null!;

        public required string Description { get; set; }
        public bool Retweeted { get; set; }
        public bool Favorited { get; set; }
        public bool Bookmarked { get; set; }
        public required string CreatedAt { get; set; }

        public List<PostHashtagEntity> Hashtags { get; set; } = [];
        public List<PostMediaEntity> Medias { get; set; } = [];
        public List<PostChangeEntity> Changes { get; set; } = [];
        public List<PostIndexEntryEntity> IndexEntries { get; set; } = [];
    }

    private sealed class PostHashtagEntity
    {
        public int Id { get; set; }
        public string PostId { get; set; } = string.Empty;
        public required string Value { get; set; }
        public int Ordinal { get; set; }
        public PostEntity Post { get; set; } = null!;
    }

    private sealed class PostMediaEntity
    {
        public int Id { get; set; }
        public string PostId { get; set; } = string.Empty;
        public required string MediaId { get; set; }
        public required string Url { get; set; }
        public required string Type { get; set; }
        public int? VideoDurationMilis { get; set; }
        public int Ordinal { get; set; }

        public PostEntity Post { get; set; } = null!;
        public List<PostMediaVariantEntity> Variants { get; set; } = [];
    }

    private sealed class PostMediaVariantEntity
    {
        public int Id { get; set; }
        public int MediaRefId { get; set; }
        public required string ContentType { get; set; }
        public int? Bitrate { get; set; }
        public required string Url { get; set; }
        public int Ordinal { get; set; }

        public PostMediaEntity Media { get; set; } = null!;
    }

    private sealed class PostIndexEntryEntity
    {
        public int Id { get; set; }
        public string PostId { get; set; } = string.Empty;
        public required string UserId { get; set; }
        public required string Origin { get; set; }
        public string? Previous { get; set; }
        public string? Next { get; set; }

        public PostEntity Post { get; set; } = null!;
    }

    private sealed class PostChangeEntity
    {
        public int Id { get; set; }
        public string PostId { get; set; } = string.Empty;
        public required string UserId { get; set; }
        public DateTime Date { get; set; }
        public required string ChangeType { get; set; }

        public PostEntity Post { get; set; } = null!;
        public List<PostChangeFieldEntity> Fields { get; set; } = [];
    }

    private sealed class PostChangeFieldEntity
    {
        public int Id { get; set; }
        public int ChangeId { get; set; }
        public required string Field { get; set; }
        public string? OldValueJson { get; set; }
        public string? NewValueJson { get; set; }

        public PostChangeEntity Change { get; set; } = null!;
    }

    private sealed class PostHashMetaEntity
    {
        public required string Id { get; set; }
        public required string Hash { get; set; }
        public bool Deleted { get; set; }
    }
}
