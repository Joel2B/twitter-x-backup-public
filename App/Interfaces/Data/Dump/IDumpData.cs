using Backup.App.Models.Config.Api;
using Backup.App.Models.Dump;

namespace Backup.App.Interfaces.Data.Post;

public interface IDumpData
{
    public string? Id { get; set; }
    public Task<DumpData?> GetData(ApiContext context);

    public Task Save(
        string response,
        List<Models.Post.Post> posts,
        string cursor,
        ApiContext context
    );

    public Task Flush(IPostData postData, string userId, ApiContext context);
}
