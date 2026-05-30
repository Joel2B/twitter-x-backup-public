using Backup.Application.Posts.Ports;
using Backup.Domain.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.Posts.Adapters;

internal sealed class PostReplicationStoreAdapter(IPostDomainDataStore store) : IPostReplicationStore
{
    private readonly IPostDomainDataStore _store = store;

    public string? Id => _store.Id;
    public bool IsDefault => _store.IsDefault;

    public Task<Dictionary<string, string>> GetHashesById() => _store.GetHashesById();
    public Task<List<Post>?> GetAll() => _store.GetAll();
    public Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids) => _store.GetByIds(ids);

    public Task Save() => _store.Save();
    public Task Prune() => _store.Prune();

    public Task Reset(List<Post> posts) => _store.Reset(posts);
    public Task UpsertPosts(List<Post> posts) => _store.UpsertPosts(posts);
}
