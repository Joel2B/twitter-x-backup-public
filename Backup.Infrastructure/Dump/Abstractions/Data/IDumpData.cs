using Backup.Infrastructure.Models.Config.Api;
using Backup.Infrastructure.Models.Dump;
using Backup.Infrastructure.Posts.Models;
using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.Dump.Abstractions.Data;

public interface IDumpData
{
    public string? Id { get; set; }
    public Task<DumpData?> GetData(ApiContext context);

    public Task Save(string response, List<Post> posts, string cursor, ApiContext context);

    public Task Flush(IPostDomainData postData, string userId, ApiContext context);
}
