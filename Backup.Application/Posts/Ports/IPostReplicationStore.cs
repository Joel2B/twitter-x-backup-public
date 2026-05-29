using Backup.Domain.Posts;

namespace Backup.Application.Posts.Ports;

public interface IPostReplicationStore
{
    string? Id { get; }
    bool IsDefault { get; }
    Task<Dictionary<string, string>> GetHashesById();
    Task<List<Post>?> GetAll();
    Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids);
    Task UpsertPosts(List<Post> posts);
    Task Reset(List<Post> posts);
    Task Save();
    Task Prune();
}

