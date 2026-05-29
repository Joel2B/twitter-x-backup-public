using Backup.Api.Errors;
using Backup.Api.Models;
using Backup.Domain.Posts;

namespace Backup.Api.Services;

public class PostIngestionService(
    Backup.Application.PostIngestion.IPostIngestionService appService
) : IPostIngestionService
{
    private readonly Backup.Application.PostIngestion.IPostIngestionService _appService = appService;

    public async Task<PostIngestResult> IngestRaw(
        string userId,
        string origin,
        string rawRequestBody
    )
    {
        try
        {
            Backup.Application.PostIngestion.Models.PostIngestResult result =
                await _appService.IngestRaw(userId, origin, rawRequestBody);

            return new PostIngestResult(
                result.ReceivedPosts,
                result.SavedPosts,
                result.NextCursor,
                MapDiagnostics(result.Diagnostics)
            );
        }
        catch (Backup.Application.PostIngestion.PostIngestionException ex)
        {
            throw new ApiException(ex.Message);
        }
    }

    public async Task<PostIngestResult> IngestProcessed(
        string userId,
        string origin,
        IReadOnlyCollection<ProcessedPostInput> posts
    )
    {
        try
        {
            Backup.Application.PostIngestion.Models.PostIngestResult result =
                await _appService.IngestProcessed(
                userId,
                origin,
                MapMany(posts)
            );

            return new PostIngestResult(
                result.ReceivedPosts,
                result.SavedPosts,
                result.NextCursor,
                MapDiagnostics(result.Diagnostics)
            );
        }
        catch (Backup.Application.PostIngestion.PostIngestionException ex)
        {
            throw new ApiException(ex.Message);
        }
    }

    private static List<Post> MapMany(IReadOnlyCollection<ProcessedPostInput> posts) =>
        posts.Select(Map).ToList();

    private static Post Map(ProcessedPostInput post) =>
        new()
        {
            Id = post.Id,
            Profile = new PostProfile
            {
                Id = post.Profile.Id,
                UserName = post.Profile.UserName,
                Name = post.Profile.Name,
                BannerUrl = post.Profile.BannerUrl,
                ImageUrl = post.Profile.ImageUrl,
                Following = post.Profile.Following,
                Count = null,
            },
            Description = post.Description,
            Retweeted = post.Retweeted ?? false,
            Favorited = post.Favorited ?? false,
            Bookmarked = post.Bookmarked ?? false,
            CreatedAt = post.CreatedAt,
            Hashtags = MapHashtags(post.Hashtags),
            Medias = MapMedias(post.Medias),
            Deleted = post.Deleted ?? false,
        };

    private static List<string>? MapHashtags(List<string>? hashtags)
    {
        List<string>? mapped = hashtags
            ?.Where(tag => !string.IsNullOrWhiteSpace(tag))
            .Select(tag => tag.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return mapped is { Count: > 0 } ? mapped : null;
    }

    private static List<PostMedia>? MapMedias(List<ProcessedPostMediaInput>? medias)
    {
        List<PostMedia>? mapped = medias?.Select(MapMedia).ToList();
        return mapped is { Count: > 0 } ? mapped : null;
    }

    private static PostMedia MapMedia(ProcessedPostMediaInput media) =>
        new()
        {
            Id = media.Id,
            Url = media.Url,
            Type = media.Type,
            VideoInfo = MapVideoInfo(media.VideoInfo),
        };

    private static PostVideoInfo? MapVideoInfo(ProcessedPostVideoInfoInput? videoInfo)
    {
        if (videoInfo is null)
            return null;

        return new PostVideoInfo
        {
            DurationMilis = videoInfo.DurationMilis,
            Variants = videoInfo.Variants?.Select(MapVariant).ToList(),
        };
    }

    private static PostVariant MapVariant(ProcessedPostVariantInput variant) =>
        new()
        {
            ContentType = variant.ContentType,
            Bitrate = variant.Bitrate,
            Url = variant.Url,
        };

    private static PostIngestDiagnostics? MapDiagnostics(
        Backup.Application.PostIngestion.Models.PostIngestDiagnostics? diagnostics
    )
    {
        if (diagnostics is null)
            return null;

        return new PostIngestDiagnostics(
            diagnostics.BeforeCount,
            diagnostics.AfterCount,
            diagnostics.DeltaCount,
            diagnostics.IgnoredPosts,
            diagnostics.DurationMs
        );
    }
}

