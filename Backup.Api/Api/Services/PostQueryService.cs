using Backup.Api.Errors;
using Backup.Api.Models;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Api.Services;

public sealed class PostQueryService(IPostData postData)
{
    private readonly IPostData _postData = postData;

    public async Task<PagedResponse<PostSummary>> GetPosts(
        PostListQuery request,
        CancellationToken cancellationToken
    )
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Post>? posts = await _postData.GetAll();
        IEnumerable<Post> query = posts ?? [];

        if (!string.IsNullOrWhiteSpace(request.ProfileId))
        {
            query = query.Where(post =>
                string.Equals(post.Profile.Id, request.ProfileId, StringComparison.Ordinal)
            );
        }

        if (!string.IsNullOrWhiteSpace(request.UserName))
        {
            query = query.Where(post =>
                string.Equals(
                    post.Profile.UserName,
                    request.UserName,
                    StringComparison.OrdinalIgnoreCase
                )
            );
        }

        if (request.Deleted.HasValue)
            query = query.Where(post => post.Deleted == request.Deleted.Value);

        if (request.HasMedia.HasValue)
        {
            query = query.Where(post => ((post.Medias?.Count ?? 0) > 0) == request.HasMedia.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.TextContains))
        {
            query = query.Where(post =>
                post.Description.Contains(request.TextContains, StringComparison.OrdinalIgnoreCase)
            );
        }

        List<Post> filtered = ApplySort(query, request.Sort).ToList();
        int page = NormalizePage(request.Page);
        int pageSize = NormalizePageSize(request.PageSize);
        int totalItems = filtered.Count;
        int totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);
        List<PostSummary> items = filtered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(MapSummary)
            .ToList();

        return new PagedResponse<PostSummary>
        {
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = totalPages,
            HasPrevious = page > 1 && totalItems > 0,
            HasNext = page < totalPages,
            Items = items,
        };
    }

    public async Task<PostDetail> GetPost(string postId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        List<Post> posts = await _postData.GetByIds([postId]);
        Post? post = posts.FirstOrDefault(item =>
            string.Equals(item.Id, postId, StringComparison.Ordinal)
        );

        if (post is null)
            throw new ApiException($"post '{postId}' was not found.");

        return MapDetail(post);
    }

    private static IEnumerable<Post> ApplySort(IEnumerable<Post> query, PostSortOption sort) =>
        sort switch
        {
            PostSortOption.IdAsc => query.OrderBy(post => post.Id, StringComparer.Ordinal),
            PostSortOption.IdDesc => query.OrderByDescending(
                post => post.Id,
                StringComparer.Ordinal
            ),
            PostSortOption.CreatedAtAsc => query
                .OrderBy(GetCreatedAtSortKey)
                .ThenBy(post => post.Id, StringComparer.Ordinal),
            _ => query
                .OrderByDescending(GetCreatedAtSortKey)
                .ThenByDescending(post => post.Id, StringComparer.Ordinal),
        };

    private static long GetCreatedAtSortKey(Post post)
    {
        if (DateTimeOffset.TryParse(post.CreatedAt, out DateTimeOffset createdAt))
            return createdAt.UtcTicks;

        return long.MinValue;
    }

    private static int NormalizePage(int page) => page <= 0 ? 1 : page;

    private static int NormalizePageSize(int pageSize) =>
        Math.Clamp(pageSize <= 0 ? 50 : pageSize, 1, 250);

    private static PostSummary MapSummary(Post post) =>
        new()
        {
            Id = post.Id,
            ProfileId = post.Profile.Id,
            UserName = post.Profile.UserName,
            DisplayName = post.Profile.Name,
            Description = post.Description,
            CreatedAt = post.CreatedAt,
            Deleted = post.Deleted,
            Retweeted = post.Retweeted,
            Favorited = post.Favorited,
            Bookmarked = post.Bookmarked,
            MediaCount = post.Medias?.Count ?? 0,
            HashtagCount = post.Hashtags?.Count ?? 0,
        };

    private static PostDetail MapDetail(Post post) =>
        new()
        {
            Id = post.Id,
            Profile = new PostProfileSummary
            {
                Id = post.Profile.Id,
                UserName = post.Profile.UserName,
                DisplayName = post.Profile.Name,
                BannerUrl = post.Profile.BannerUrl,
                ImageUrl = post.Profile.ImageUrl,
                Following = post.Profile.Following,
                MediaCount = post.Profile.Count?.Media,
            },
            Description = post.Description,
            CreatedAt = post.CreatedAt,
            Deleted = post.Deleted,
            Retweeted = post.Retweeted,
            Favorited = post.Favorited,
            Bookmarked = post.Bookmarked,
            Hashtags = post.Hashtags ?? [],
            Medias =
                post.Medias?.Select(media => new PostMediaDetail
                    {
                        Id = media.Id,
                        Url = media.Url,
                        Type = media.Type,
                        DurationMilis = media.VideoInfo?.DurationMilis,
                        Variants = media.VideoInfo?.Variants is null
                            ? []
                            : media
                                .VideoInfo.Variants.Select(variant => new PostMediaVariantDetail
                                {
                                    ContentType = variant.ContentType,
                                    Bitrate = variant.Bitrate,
                                    Url = variant.Url,
                                })
                                .ToList(),
                    })
                    .ToList() ?? [],
        };
}
