using Backup.Infrastructure.Posts.Models.Stored;

namespace Backup.Infrastructure.Posts.Abstractions.Data;

public interface IPostData
{
    public string? Id { get; set; }
    public Task<int> GetCount();
    public Task<List<Post>?> GetAll();
    public Task<List<MediaInput>?> GetMediaInputs();
    public Task<Dictionary<string, string>> GetHashesById();
    public Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids);

    public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    );

    public Task AddPosts(
        string userId,
        string origin,
        List<Post> incoming,
        MergeOptions? options = null
    );

    public Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    );

    public Task Reset(List<Post> posts);
    public Task UpsertPosts(List<Post> posts);
    public Task Save();
    public Task Prune();
}
