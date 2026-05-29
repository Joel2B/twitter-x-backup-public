using Backup.App.Api.Errors;
using Backup.App.Api.Models;

namespace Backup.App.Api.Services;

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

    private static List<Backup.Application.PostIngestion.Models.ProcessedPostInput> MapMany(
        IReadOnlyCollection<ProcessedPostInput> posts
    ) => posts.Select(Map).ToList();

    private static Backup.Application.PostIngestion.Models.ProcessedPostInput Map(
        ProcessedPostInput post
    ) =>
        new()
        {
            Id = post.Id,
            Profile = new Backup.Application.PostIngestion.Models.ProcessedPostProfileInput
            {
                Id = post.Profile.Id,
                UserName = post.Profile.UserName,
                Name = post.Profile.Name,
                BannerUrl = post.Profile.BannerUrl,
                ImageUrl = post.Profile.ImageUrl,
                Following = post.Profile.Following,
            },
            Description = post.Description,
            Retweeted = post.Retweeted,
            Favorited = post.Favorited,
            Bookmarked = post.Bookmarked,
            CreatedAt = post.CreatedAt,
            Hashtags = post.Hashtags is null ? null : [.. post.Hashtags],
            Medias = post.Medias?.Select(MapMedia).ToList(),
            Deleted = post.Deleted,
        };

    private static Backup.Application.PostIngestion.Models.ProcessedPostMediaInput MapMedia(
        ProcessedPostMediaInput media
    ) =>
        new()
        {
            Id = media.Id,
            Url = media.Url,
            Type = media.Type,
            VideoInfo = media.VideoInfo is null
                ? null
                : new Backup.Application.PostIngestion.Models.ProcessedPostVideoInfoInput
                {
                    DurationMilis = media.VideoInfo.DurationMilis,
                    Variants = media.VideoInfo.Variants?.Select(MapVariant).ToList(),
                },
        };

    private static Backup.Application.PostIngestion.Models.ProcessedPostVariantInput MapVariant(
        ProcessedPostVariantInput variant
    ) =>
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
