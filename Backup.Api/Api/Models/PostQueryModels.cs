using Backup.Api.Binding;
using Microsoft.AspNetCore.Mvc;

namespace Backup.Api.Models;

public sealed class PostListQuery
{
    public string? ProfileId { get; init; }
    public string? UserName { get; init; }
    public bool? Deleted { get; init; }
    public bool? HasMedia { get; init; }
    public string? TextContains { get; init; }

    [ModelBinder(BinderType = typeof(NumericEnumModelBinder))]
    public PostSortOption Sort { get; init; } = PostSortOption.CreatedAtDesc;
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 50;
}

public enum PostSortOption
{
    CreatedAtDesc = 1,
    CreatedAtAsc = 2,
    IdDesc = 3,
    IdAsc = 4,
}

public sealed class PostSummary
{
    public required string Id { get; init; }
    public required string ProfileId { get; init; }
    public required string? UserName { get; init; }
    public required string? DisplayName { get; init; }
    public required string Description { get; init; }
    public required string CreatedAt { get; init; }
    public required bool Deleted { get; init; }
    public required bool Retweeted { get; init; }
    public required bool Favorited { get; init; }
    public required bool Bookmarked { get; init; }
    public required int MediaCount { get; init; }
    public required int HashtagCount { get; init; }
}

public sealed class PostDetail
{
    public required string Id { get; init; }
    public required PostProfileSummary Profile { get; init; }
    public required string Description { get; init; }
    public required string CreatedAt { get; init; }
    public required bool Deleted { get; init; }
    public required bool Retweeted { get; init; }
    public required bool Favorited { get; init; }
    public required bool Bookmarked { get; init; }
    public required IReadOnlyList<string> Hashtags { get; init; }
    public required IReadOnlyList<PostMediaDetail> Medias { get; init; }
}

public sealed class PostProfileSummary
{
    public required string Id { get; init; }
    public required string? UserName { get; init; }
    public required string? DisplayName { get; init; }
    public required string? BannerUrl { get; init; }
    public required string? ImageUrl { get; init; }
    public required bool? Following { get; init; }
    public required int? MediaCount { get; init; }
}

public sealed class PostMediaDetail
{
    public required string Id { get; init; }
    public required string Url { get; init; }
    public required string Type { get; init; }
    public required int? DurationMilis { get; init; }
    public required IReadOnlyList<PostMediaVariantDetail> Variants { get; init; }
}

public sealed class PostMediaVariantDetail
{
    public required string ContentType { get; init; }
    public required int? Bitrate { get; init; }
    public required string Url { get; init; }
}
