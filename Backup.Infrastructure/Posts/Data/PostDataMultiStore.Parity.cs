using Backup.Infrastructure.Interfaces.Data.Posts;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Posts.Adapters;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Data.Posts;

public partial class PostDataMultiStore
{
    private async Task<Backup.Domain.Posts.PostStoreParityResult> VerifyStoreCountsInternal()
    {
        List<PostStoreCountSourceAdapter> adapters = _stores
            .Select(store =>
                new PostStoreCountSourceAdapter(
                    store as IPostDomainDataStore ?? new PostDataDomainStoreAdapter(store)
                )
            )
            .ToList();

        return await _postStoreParityService.Verify(adapters);
    }

    private void LogStoreSnapshotCounts(Backup.Domain.Posts.PostStoreParityResult parity)
    {
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
    }

    private void LogStoreParity(Backup.Domain.Posts.PostStoreParityResult parity)
    {
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
