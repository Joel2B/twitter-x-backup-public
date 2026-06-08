namespace Backup.Api.Models;

public sealed class MediaInputsQuery
{
    public string? PostId { get; init; }
    public string? ProfileId { get; init; }
    public bool? Deleted { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class MediaDownloadsQuery
{
    public bool FilteredOnly { get; init; } = true;
    public string? PostId { get; init; }
    public string? ProfileId { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;
}

public sealed class MediaFilesQuery
{
    public bool FilteredOnly { get; init; } = true;
    public string? PostId { get; init; }
    public string? ProfileId { get; init; }
    public string? PathContains { get; init; }
    public string? UrlContains { get; init; }
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public sealed class MediaQuerySummaryResponse
{
    public required bool FilteredOnly { get; init; }
    public required int MediaInputCount { get; init; }
    public required int DownloadCount { get; init; }
    public required int FileCount { get; init; }
    public required int StorageCount { get; init; }
    public required IReadOnlyList<string> Storages { get; init; }
}

public sealed class PagedResponse<T>
{
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public required int TotalItems { get; init; }
    public required int TotalPages { get; init; }
    public required bool HasPrevious { get; init; }
    public required bool HasNext { get; init; }
    public required IReadOnlyList<T> Items { get; init; }
}

public sealed class MediaInputSummary
{
    public required string PostId { get; init; }
    public required string ProfileId { get; init; }
    public required string? UserName { get; init; }
    public required string? DisplayName { get; init; }
    public required bool Deleted { get; init; }
    public required int MediaCount { get; init; }
    public required IReadOnlyList<string> MediaTypes { get; init; }
    public required bool HasProfileImage { get; init; }
    public required bool HasBannerImage { get; init; }
}

public sealed class MediaDownloadGroupSummary
{
    public required string PostId { get; init; }
    public required string? ProfileId { get; init; }
    public required string? UserName { get; init; }
    public required string? DisplayName { get; init; }
    public required bool? Deleted { get; init; }
    public required int FileCount { get; init; }
    public required IReadOnlyList<MediaFileSummary> Items { get; init; }
}

public sealed class MediaFileSummary
{
    public required string PostId { get; init; }
    public required string? ProfileId { get; init; }
    public required string? UserName { get; init; }
    public required string Url { get; init; }
    public required string Path { get; init; }
    public required IReadOnlyList<MediaStorageFileState> Storages { get; init; }
}

public sealed class MediaStorageFileState
{
    public required string StorageId { get; init; }
    public required bool Exists { get; init; }
    public required int? PartitionId { get; init; }
    public required long? StreamSize { get; init; }
    public required long? FileSize { get; init; }
}
