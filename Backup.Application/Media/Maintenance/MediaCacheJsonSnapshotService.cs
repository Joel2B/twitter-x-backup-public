using Backup.Application.Media.Maintenance.Models;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheJsonSnapshotService : IMediaCacheJsonSnapshotService
{
    public long? ParseNullableLong(object? value)
    {
        if (value is null)
            return null;

        if (value is long longValue)
            return longValue;

        if (value is int intValue)
            return intValue;

        if (value is string textValue && long.TryParse(textValue, out long parsedLong))
            return parsedLong;

        return null;
    }

    public int? ParseNullableInt(object? value)
    {
        if (value is null)
            return null;

        if (value is int intValue)
            return intValue;

        if (value is long longValue)
            return checked((int)longValue);

        if (value is string textValue && int.TryParse(textValue, out int parsedInt))
            return parsedInt;

        return null;
    }

    public MediaCacheJsonSnapshot? CreateSnapshot(
        string? path,
        long? streamSizeBytes,
        long? fileSizeBytes,
        int? partitionId
    )
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        return new MediaCacheJsonSnapshot
        {
            Path = path.Trim(),
            StreamSizeBytes = streamSizeBytes,
            FileSizeBytes = fileSizeBytes,
            PartitionId = partitionId,
        };
    }

    public IReadOnlyList<MediaCacheJsonSnapshot> PrepareForWrite(
        IEnumerable<MediaCacheJsonSnapshot> snapshots
    ) =>
        snapshots
            .Where(snapshot => !string.IsNullOrWhiteSpace(snapshot.Path))
            .GroupBy(snapshot => snapshot.Path, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Last())
            .OrderBy(snapshot => snapshot.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
}
