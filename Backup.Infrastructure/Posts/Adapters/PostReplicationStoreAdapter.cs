using Backup.Application.Posts.Ports;
using Backup.Domain.Posts;
using Backup.Infrastructure.Interfaces.Data.Posts;

namespace Backup.Infrastructure.Posts.Adapters;

internal sealed class PostReplicationStoreAdapter(IPostDataStore store) : IPostReplicationStore
{
    private readonly IPostDataStore _store = store;

    public string? Id => _store.Id;
    public bool IsDefault => _store.IsDefault;

    public Task<Dictionary<string, string>> GetHashesById() => _store.GetHashesById();

    public async Task<List<Post>?> GetAll()
    {
        List<Backup.Infrastructure.Models.Posts.Post>? posts = await _store.GetAll();
        return posts?.Select(PostReplicationMapper.ToDomain).ToList();
    }

    public async Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids)
    {
        List<Backup.Infrastructure.Models.Posts.Post> posts = await _store.GetByIds(ids);
        return posts.Select(PostReplicationMapper.ToDomain).ToList();
    }

    public Task Save() => _store.Save();
    public Task Prune() => _store.Prune();

    public Task Reset(List<Post> posts) =>
        _store.Reset(posts.Select(PostReplicationMapper.ToApp).ToList());

    public Task UpsertPosts(List<Post> posts) =>
        _store.UpsertPosts(posts.Select(PostReplicationMapper.ToApp).ToList());
}

