using Backup.Infrastructure.Logging;
using Backup.Application.Posts;
using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Interfaces.Services.Posts;
using Backup.Infrastructure.Models.Posts;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Data.Posts;

public class PostDataMultiStore(
    IEnumerable<IPostDataStore> stores,
    IPostReplication replication,
    IPostStoreParityService postStoreParityService,
    ILogger<PostDataMultiStore> logger
) : IPostData
{
    private readonly List<IPostDataStore> _stores = [.. stores];
    private readonly IPostReplication _replication = replication;
    private readonly IPostStoreParityService _postStoreParityService = postStoreParityService;
    private readonly ILogger<PostDataMultiStore> _logger = logger;

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

    public Task<List<Post>?> GetAll() => Primary.GetAll();

    public Task<List<MediaInput>?> GetMediaInputs() => Primary.GetMediaInputs();

    public Task<Dictionary<string, string>> GetHashesById() => Primary.GetHashesById();

    public Task<List<Post>> GetByIds(IReadOnlyCollection<string> ids) => Primary.GetByIds(ids);

    public Task<Dictionary<string, int>> GetPostCountsByProfileIds(
        IReadOnlyCollection<string> profileIds
    ) => Primary.GetPostCountsByProfileIds(profileIds);

    public Task AddPosts(
        string userId,
        string origin,
        List<Post> incoming,
        MergeOptions? options = null
    ) => Primary.AddPosts(userId, origin, incoming, options);

    public Task<int> MarkDeletedExcept(
        string userId,
        string origin,
        IReadOnlyCollection<string> keepPostIds
    ) => Primary.MarkDeletedExcept(userId, origin, keepPostIds);

    public Task Reset(List<Post> posts) => Primary.Reset(posts);

    public Task UpsertPosts(List<Post> posts) => Primary.UpsertPosts(posts);

    public async Task Save()
    {
        await Primary.Save();

        if (_stores.Count <= 1)
            return;

        await _replication.Replicate(_stores);
    }

    public Task Prune() => Primary.Prune();

    public async Task VerifyStoreCounts()
    {
        if (_stores.Count <= 1)
        {
            _logger.LogInfo(
                "post store parity skipped: only {count} enabled post store",
                _stores.Count
            );
            return;
        }
        List<PostStoreCountSourceAdapter> adapters = _stores
            .Select(store => new PostStoreCountSourceAdapter(new PostDataDomainStoreAdapter(store)))
            .ToList();
        Backup.Domain.Posts.PostStoreParityResult parity = await _postStoreParityService.Verify(
            adapters
        );

        foreach (Backup.Domain.Posts.PostStoreSnapshot snapshot in parity.Snapshots)
        {
            _logger.LogInfo(
                "post store counts [{storeId}] posts={posts}, profiles={profiles}, hashtags={hashtags}, medias={medias}, mediaVariants={mediaVariants}, indexEntries={indexEntries}, changes={changes}, changeFields={changeFields}, hashMeta={hashMeta}",
                snapshot.Label,
                snapshot.Counts.Posts,
                snapshot.Counts.Profiles,
                snapshot.Counts.Hashtags,
                snapshot.Counts.Medias,
                snapshot.Counts.MediaVariants,
                snapshot.Counts.IndexEntries,
                snapshot.Counts.Changes,
                snapshot.Counts.ChangeFields,
                snapshot.Counts.HashMeta
            );
        }

        if (parity.Mismatches.Count == 0)
        {
            foreach (
                Backup.Domain.Posts.PostStoreSnapshot snapshot in parity.Snapshots.Where(snapshot =>
                    !string.Equals(snapshot.Label, parity.PrimaryLabel, StringComparison.Ordinal)
                )
            )
            {
                _logger.LogInfo(
                    "post store parity OK: primary={primary} secondary={secondary}",
                    parity.PrimaryLabel,
                    snapshot.Label
                );
            }
            return;
        }

        foreach (Backup.Domain.Posts.PostStoreMismatch mismatch in parity.Mismatches)
        {
            _logger.LogWarning(
                "post store parity MISMATCH: primary={primary} secondary={secondary} diffs={diffs}",
                mismatch.PrimaryLabel,
                mismatch.SecondaryLabel,
                string.Join(", ", mismatch.Diffs)
            );
        }
    }
}


