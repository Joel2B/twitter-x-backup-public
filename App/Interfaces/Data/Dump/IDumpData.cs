using Backup.App.Models.Dump;

namespace Backup.App.Interfaces.Data.Post;

public interface IDumpData
{
    public string? Id { get; set; }
    public Task<DumpData?> GetData();
    public Task Save(string response, List<Models.Post.Post> posts, string cursor);
    public Task<Dictionary<string, Models.Post.Post>> Flush(string userId);
}
