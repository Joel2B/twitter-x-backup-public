using Backup.Api.Models;

namespace Backup.Api.Services;

public interface IPostIngestionService
{
    Task<PostIngestResult> IngestRaw(
        string userId,
        string origin,
        string rawRequestBody,
        CancellationToken cancellationToken = default
    );

    Task<PostIngestResult> IngestProcessed(
        string userId,
        string origin,
        IReadOnlyCollection<ProcessedPostInput> posts,
        CancellationToken cancellationToken = default
    );
}
