using Backup.Application.Media.Backup;
using Backup.Application.Media.Backup.Models;

namespace Backup.Tests;

public sealed class MediaBackupPartitionPathServiceTests
{
    [Fact]
    public void GetRequiredBackupRootPath_ReturnsBackupPartitionPath()
    {
        MediaBackupPartitionPathService sut = new();
        List<MediaBackupPartitionPathCandidate> partitions =
        [
            new() { Type = "primary", RootPath = "/primary" },
            new() { Type = "BACKUP", RootPath = "/backup" },
        ];

        string path = sut.GetRequiredBackupRootPath(partitions);

        Assert.Equal("/backup", path);
    }

    [Fact]
    public void GetRequiredBackupRootPath_ThrowsWhenMissingBackupPartition()
    {
        MediaBackupPartitionPathService sut = new();
        List<MediaBackupPartitionPathCandidate> partitions =
        [
            new() { Type = "primary", RootPath = "/primary" },
        ];

        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(() =>
            sut.GetRequiredBackupRootPath(partitions)
        );

        Assert.Equal("Backup partition not configured.", ex.Message);
    }
}
