using Backup.App.Interfaces.Data.Post;
using Backup.App.Interfaces.Services.Post;

namespace Backup.App.Data.Post;

public class PostDataMultiStore(IEnumerable<IPostDataStore> stores, IPostReplication replication)
    : IPostData
{
    private readonly List<IPostDataStore> _stores = [.. stores];
    private readonly IPostReplication _replication = replication;

    private IPostDataStore Primary
    {
        get
        {
            if (_stores.Count == 0)
                throw new InvalidOperationException("No post data stores are configured.");

            List<IPostDataStore> defaults = _stores.Where(store => store.IsDefault).ToList();

            if (defaults.Count > 1)
                throw new InvalidOperationException(
                    "Only one post data store can be marked as default."
                );

            return defaults.FirstOrDefault() ?? _stores.First();
        }
    }

    public string? Id
    {
        get => Primary.Id;
        set => Primary.Id = value;
    }

    public Task<int> GetCount() => Primary.GetCount();

    public Task<List<Models.Post.Post>?> GetAll() => Primary.GetAll();

    public Task<List<Models.Post.MediaInput>?> GetMediaInputs() => Primary.GetMediaInputs();

    public Task<Dictionary<string, string>> GetHashesById() => Primary.GetHashesById();

    public Task<List<Models.Post.Post>> GetByIds(IReadOnlyCollection<string> ids) =>
        Primary.GetByIds(ids);

    public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    ) => Primary.GetPostCountsByProfileIds(profileIds);

    public Task AddPosts(
        string userId,
        string origin,
        List<Models.Post.Post> incoming,
        Models.Post.MergeOptions? options = null
    ) => Primary.AddPosts(userId, origin, incoming, options);

    public Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    ) => Primary.MarkDeletedExcept(userId, origin, keepPostIds);

    public Task Reset(List<Models.Post.Post> posts) => Primary.Reset(posts);

    public Task UpsertPosts(List<Models.Post.Post> posts) => Primary.UpsertPosts(posts);

    public async Task Save()
    {
        await Primary.Save();

        if (_stores.Count <= 1)
            return;

        await _replication.Replicate(_stores.Cast<IPostData>());
    }

    public Task Prune() => Primary.Prune();
}
