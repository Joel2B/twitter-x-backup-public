using Backup.Api.Models;

namespace Backup.Api.Services;

public interface IPostIngestionService
{
    Task<PostIngestResult> IngestRaw(string userId, string origin, string rawRequestBody);

    Task<PostIngestResult> IngestProcessed(
        string userId,
        string origin,
        IReadOnlyCollection<ProcessedPostInput> posts
    );
}

