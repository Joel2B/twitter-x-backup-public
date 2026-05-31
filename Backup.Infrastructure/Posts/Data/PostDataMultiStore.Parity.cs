using Backup.Infrastructure.Posts.Abstractions.Data;
using Backup.Infrastructure.Logging;
using Backup.Infrastructure.Posts.Adapters;
using Backup.Application.Posts.Models;
using Microsoft.Extensions.Logging;

namespace Backup.Infrastructure.Posts.Data;

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

    private void LogStoreSnapshotCounts(PostStoreParityReport report)
    {
        foreach (PostStoreParitySnapshotItem snapshot in report.Snapshots)
        {
            _logger.LogInfo(
                "post store counts [{storeId}] posts={posts}, profiles={profiles}, hashtags={hashtags}, medias={medias}, mediaVariants={mediaVariants}, indexEntries={indexEntries}, changes={changes}, changeFields={changeFields}, hashMeta={hashMeta}",
                snapshot.Label,
                snapshot.Posts,
                snapshot.Profiles,
                snapshot.Hashtags,
                snapshot.Medias,
                snapshot.MediaVariants,
                snapshot.IndexEntries,
                snapshot.Changes,
                snapshot.ChangeFields,
                snapshot.HashMeta
            );
        }
    }

    private void LogStoreParity(PostStoreParityReport report)
    {
        foreach (PostStoreParityStatusItem status in report.Statuses)
        {
            if (!status.IsMismatch)
            {
                _logger.LogInfo(
                    "post store parity OK: primary={primary} secondary={secondary}",
                    status.PrimaryLabel,
                    status.SecondaryLabel
                );
                continue;
            }

            _logger.LogWarning(
                "post store parity MISMATCH: primary={primary} secondary={secondary} diffs={diffs}",
                status.PrimaryLabel,
                status.SecondaryLabel,
                status.DiffsText
            );
        }
    }
}
