using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;

namespace Backup.Tests;

public sealed class MediaBackupChunkMetadataRefreshPlanningServiceTests
{
    [Fact]
    public void Plan_Updates_Only_Candidates_With_Entry()
    {
        MediaBackupChunkMetadataPolicyService metadataPolicy = new();
        MediaBackupChunkMetadataRefreshPlanningService sut = new(metadataPolicy);

        var plan = sut.Plan(
            [
                new MediaBackupChunkMetadataRefreshCandidate
                {
                    Path = "a.jpg",
                    HasEntry = true,
                    Current = new MediaBackupChunkDataMetadata { FileSize = null, Crc32 = null },
                    Entry = new MediaBackupChunkDataMetadata { FileSize = 123, Crc32 = 999 },
                },
                new MediaBackupChunkMetadataRefreshCandidate
                {
                    Path = "b.jpg",
                    HasEntry = false,
                    Current = new MediaBackupChunkDataMetadata { FileSize = 1, Crc32 = 2 },
                    Entry = new MediaBackupChunkDataMetadata { FileSize = 3, Crc32 = 4 },
                },
            ]
        );

        Assert.Single(plan.Updates);
        Assert.Equal("a.jpg", plan.Updates[0].Path);
        Assert.Equal(123L, plan.Updates[0].Metadata.FileSize);
        Assert.Equal((uint)999, plan.Updates[0].Metadata.Crc32);
    }
}
