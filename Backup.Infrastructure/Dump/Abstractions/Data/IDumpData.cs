using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Posts.Models;

namespace Backup.Infrastructure.Dump.Abstractions.Data;

public interface IDumpData
{
    public string? Id { get; set; }
    public Task<DumpData?> GetData(
        ApiContext context,
        CancellationToken cancellationToken = default
    );

    public Task Save(
        string response,
        List<Post> posts,
        string cursor,
        ApiContext context,
        CancellationToken cancellationToken = default
    );

    public Task Flush(
        IPostDomainData postData,
        string userId,
        ApiContext context,
        CancellationToken cancellationToken = default
    );
}
