using Backup.Application.Bulk.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Bulk.Ports;

public interface IBulkPhase1Command
{
    Task<int> GetPostCount();
    Task<IReadOnlyList<BulkItem>> GetBulks();
    Task SaveBulks(IReadOnlyList<BulkItem> bulks);
    Task<bool> VerifyApi();
    Task<ParseResult?> GetUserMedia(
        string userId,
        string origin,
        int count,
        string? cursor,
        CancellationToken cancellationToken
    );
    Task AddPosts(string userId, string origin, List<Post> posts);
    Task SavePosts();
}
