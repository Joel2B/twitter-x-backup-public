using Backup.Application.PostIngestion.Models;
using Backup.Domain.Posts;

namespace Backup.Application.PostIngestion;

public interface IPostIngestionService
{
    Task<PostIngestResult> IngestRaw(string userId, string origin, string rawRequestBody);

    Task<PostIngestResult> IngestProcessed(
        string userId,
        string origin,
        IReadOnlyCollection<Post> posts
    );
}
