using Backup.Api.Errors;
using Backup.Api.Models;

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
                ProcessedPostInputMapper.MapMany(posts)
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
