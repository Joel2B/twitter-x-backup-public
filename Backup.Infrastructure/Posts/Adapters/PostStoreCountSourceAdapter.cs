using Backup.Application.Posts.Models;
using Backup.Application.Posts.Ports;
using Backup.Infrastructure.Interfaces.Data.Posts;

namespace Backup.Infrastructure.Posts.Adapters;

internal sealed class PostStoreCountSourceAdapter(IPostDataStore store) : IPostStoreCountSource
{
    private readonly IPostDataStore _store = store;

    public string Label => string.IsNullOrWhiteSpace(_store.Id) ? _store.GetType().Name : _store.Id!;
    public bool IsDefault => _store.IsDefault;

    public async Task<PostStoreCounts> GetStoreCounts()
    {
        Backup.Infrastructure.Models.Posts.PostStoreCounts counts = await _store.GetStoreCounts();
        return new PostStoreCounts
        {
            Posts = counts.Posts,
            Profiles = counts.Profiles,
            Hashtags = counts.Hashtags,
            Medias = counts.Medias,
            MediaVariants = counts.MediaVariants,
            IndexEntries = counts.IndexEntries,
            Changes = counts.Changes,
            ChangeFields = counts.ChangeFields,
            HashMeta = counts.HashMeta
        };
    }
}

