using Backup.Domain.Posts;

namespace Backup.Infrastructure.Posts.Abstractions.Data;

public interface IPostDomainData
{
    string? Id { get; set; }
    Task<int> GetCount();
    Task<List<Post>?> GetAll();
    Task<List<MediaInput>?> GetMediaInputs();
    Task<Dictionary<string, string>> GetHashesById();
    Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids);

    Task<Dictionary<string, int>> GetPostCountsByProfileIds(IReadOnlyCollection<string> profileIds);

    Task AddPosts(string userId, string origin, List<Post> incoming, MergeOptions? options = null);

    Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    );

    Task Reset(List<Post> posts);
    Task UpsertPosts(List<Post> posts);
    Task Save();
    Task Prune();
}
