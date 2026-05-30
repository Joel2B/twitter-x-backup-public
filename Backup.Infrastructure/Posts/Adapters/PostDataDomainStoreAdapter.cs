using Backup.Domain.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.Posts.Adapters;

internal sealed class PostDataDomainStoreAdapter(IPostDataStore store) : IPostDomainDataStore
{
    private readonly IPostDataStore _store = store;

    public string? Id
    {
        get => _store.Id;
        set => _store.Id = value;
    }

    public bool IsDefault => _store.IsDefault;

    public Task<int> GetCount() => _store.GetCount();

    public async Task<List<Post>?> GetAll()
    {
        List<Backup.Infrastructure.Posts.Models.Post>? posts = await _store.GetAll();
        return posts?.Select(PostReplicationMapper.ToDomain).ToList();
    }

    public async Task<List<MediaInput>?> GetMediaInputs()
    {
        List<Backup.Infrastructure.Posts.Models.MediaInput>? inputs = await _store.GetMediaInputs();
        return inputs?.Select(PostReplicationMapper.ToDomain).ToList();
    }

    public Task<Dictionary<string, string>> GetHashesById() => _store.GetHashesById();

    public async Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids)
    {
        List<Backup.Infrastructure.Posts.Models.Post> posts = await _store.GetByIds(ids);
        return posts.Select(PostReplicationMapper.ToDomain).ToList();
    }

    public Task<Dictionary<string, int>> GetPostCountsByProfileIds(IReadOnlyCollection<string> profileIds) =>
        _store.GetPostCountsByProfileIds(profileIds);

    public Task AddPosts(string userId, string origin, List<Post> incoming, MergeOptions? options = null) =>
        _store.AddPosts(
            userId,
            origin,
            incoming.Select(PostReplicationMapper.ToApp).ToList(),
            PostReplicationMapper.ToApp(options)
        );

    public Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    ) => _store.MarkDeletedExcept(userId, origin, keepPostIds);

    public Task Reset(List<Post> posts) =>
        _store.Reset(posts.Select(PostReplicationMapper.ToApp).ToList());

    public Task UpsertPosts(List<Post> posts) =>
        _store.UpsertPosts(posts.Select(PostReplicationMapper.ToApp).ToList());

    public Task Save() => _store.Save();
    public Task Prune() => _store.Prune();

    public async Task<PostStoreCounts> GetStoreCounts()
    {
        Backup.Infrastructure.Posts.Models.PostStoreCounts counts = await _store.GetStoreCounts();
        return PostReplicationMapper.ToDomain(counts);
    }
}
