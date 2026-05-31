using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheJsonSnapshotService
{
    long? ParseNullableLong(object? value);
    int? ParseNullableInt(object? value);
    MediaCacheJsonSnapshot? CreateSnapshot(
        string? path,
        long? streamSizeBytes,
        long? fileSizeBytes,
        int? partitionId
    );
    IReadOnlyList<MediaCacheJsonSnapshot> PrepareForWrite(
        IEnumerable<MediaCacheJsonSnapshot> snapshots
    );
}
