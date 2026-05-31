using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;

namespace Backup.Tests;

public class MediaBackupChunkAssignmentServiceTests
{
    [Fact]
    public void Assign_SkipsAssignedAndOversized_AndDistributesAcrossChunks()
    {
        MediaBackupChunkAssignmentService sut = new();
        MediaBackupChunkState[] chunks =
        [
            new() { Id = 0, PathCount = 0, SizeBytes = 100 },
            new() { Id = 1, PathCount = 0, SizeBytes = 50 },
        ];
        MediaBackupPathCandidate[] candidates =
        [
            new()
            {
                OriginalPath = "a",
                CachePath = "a",
                FileSizeBytes = 10,
                IsAlreadyAssigned = false,
            },
            new()
            {
                OriginalPath = "b",
                CachePath = "b",
                FileSizeBytes = 9999,
                IsAlreadyAssigned = false,
            },
            new()
            {
                OriginalPath = "c",
                CachePath = "c",
                FileSizeBytes = 10,
                IsAlreadyAssigned = true,
            },
            new()
            {
                OriginalPath = "d",
                CachePath = "d",
                FileSizeBytes = null,
                IsAlreadyAssigned = false,
            },
        ];

        MediaBackupChunkAssignmentResult result = sut.Assign(
            chunks,
            candidates,
            totalChunkCount: 2,
            pathsPerChunk: 1,
            increaseCount: 0,
            maxPathSizeBytes: 100
        );

        Assert.Equal(1, result.InitialChunkId);
        Assert.Equal(2, result.Assignments.Count);
        Assert.Equal(1, result.Assignments[0].ChunkId);
        Assert.Equal(0, result.Assignments[1].ChunkId);
        Assert.Equal("a", result.Assignments[0].OriginalPath);
        Assert.Equal("d", result.Assignments[1].OriginalPath);
    }
}
