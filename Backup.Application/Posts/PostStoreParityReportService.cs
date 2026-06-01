using Backup.Application.Posts.Models;
using Backup.Domain.Posts;

namespace Backup.Application.Posts;

public sealed class PostStoreParityReportService : IPostStoreParityReportService
{
    public PostStoreParityReport Build(PostStoreParityResult parity)
    {
        IReadOnlyList<PostStoreParitySnapshotItem> snapshots = parity
            .Snapshots.Select(snapshot => new PostStoreParitySnapshotItem
            {
                Label = snapshot.Label,
                Posts = snapshot.Counts.Posts,
                Profiles = snapshot.Counts.Profiles,
                Hashtags = snapshot.Counts.Hashtags,
                Medias = snapshot.Counts.Medias,
                MediaVariants = snapshot.Counts.MediaVariants,
                IndexEntries = snapshot.Counts.IndexEntries,
                Changes = snapshot.Counts.Changes,
                ChangeFields = snapshot.Counts.ChangeFields,
                HashMeta = snapshot.Counts.HashMeta,
            })
            .ToList();

        IReadOnlyList<PostStoreParityStatusItem> statuses =
            parity.Mismatches.Count == 0
                ? parity
                    .Snapshots.Where(snapshot =>
                        !string.Equals(
                            snapshot.Label,
                            parity.PrimaryLabel,
                            StringComparison.Ordinal
                        )
                    )
                    .Select(snapshot => new PostStoreParityStatusItem
                    {
                        PrimaryLabel = parity.PrimaryLabel,
                        SecondaryLabel = snapshot.Label,
                        IsMismatch = false,
                        DiffsText = string.Empty,
                    })
                    .ToList()
                : parity
                    .Mismatches.Select(mismatch => new PostStoreParityStatusItem
                    {
                        PrimaryLabel = mismatch.PrimaryLabel,
                        SecondaryLabel = mismatch.SecondaryLabel,
                        IsMismatch = true,
                        DiffsText = string.Join(", ", mismatch.Diffs),
                    })
                    .ToList();

        return new PostStoreParityReport { Snapshots = snapshots, Statuses = statuses };
    }
}
