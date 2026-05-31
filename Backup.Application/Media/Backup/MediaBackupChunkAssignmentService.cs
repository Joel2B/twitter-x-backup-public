using Backup.Application.Media.Backup.Models;

namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkAssignmentService : IMediaBackupChunkAssignmentService
{
    public MediaBackupChunkAssignmentResult Assign(
        IReadOnlyList<MediaBackupChunkState> chunks,
        IReadOnlyList<MediaBackupPathCandidate> candidates,
        int totalChunkCount,
        int pathsPerChunk,
        int increaseCount,
        long maxPathSizeBytes
    )
    {
        Dictionary<int, int> counts = [];
        Dictionary<int, long> sizes = [];

        for (int i = 0; i < totalChunkCount; i++)
        {
            MediaBackupChunkState? existing = chunks.FirstOrDefault(chunk => chunk.Id == i);
            counts[i] = existing?.PathCount ?? 0;
            sizes[i] = existing?.SizeBytes ?? 0;
        }

        if (counts.Count == 0)
        {
            counts[0] = 0;
            sizes[0] = 0;
        }

        int capacity = pathsPerChunk + increaseCount;
        int currentChunk = sizes.MinBy(pair => pair.Value).Key;
        int initialChunk = currentChunk;
        List<MediaBackupPathAssignment> assignments = [];

        foreach (MediaBackupPathCandidate candidate in candidates)
        {
            if (candidate.IsAlreadyAssigned)
                continue;

            if (candidate.FileSizeBytes is long size && size > maxPathSizeBytes)
                continue;

            while (counts[currentChunk] >= capacity)
            {
                currentChunk++;

                if (currentChunk >= totalChunkCount)
                {
                    currentChunk = 0;

                    KeyValuePair<int, int> min = counts.MinBy(pair => pair.Value);
                    KeyValuePair<int, int> max = counts.MaxBy(pair => pair.Value);

                    if (min.Value != max.Value)
                        currentChunk = min.Key;
                }
            }

            assignments.Add(
                new MediaBackupPathAssignment
                {
                    ChunkId = currentChunk,
                    OriginalPath = candidate.OriginalPath,
                    CachePath = candidate.CachePath,
                }
            );

            counts[currentChunk]++;
        }

        return new MediaBackupChunkAssignmentResult
        {
            InitialChunkId = initialChunk,
            Assignments = assignments,
        };
    }
}
