using Backup.Application.Posts.Ports;
using Backup.Domain.Posts;
using Backup.Infrastructure.Posts.Abstractions.Data;

namespace Backup.Infrastructure.Posts.Adapters;

internal sealed class PostStoreCountSourceAdapter(IPostDomainDataStore store)
    : IPostStoreCountSource
{
    private readonly IPostDomainDataStore _store = store;

    public string Label =>
        string.IsNullOrWhiteSpace(_store.Id) ? _store.GetType().Name : _store.Id!;
    public bool IsDefault => _store.IsDefault;

    public Task<PostStoreCounts> GetStoreCounts() => _store.GetStoreCounts();
}
