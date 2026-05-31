using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;
using Backup.Application.Media.Models;

namespace Backup.Tests;

public sealed class MediaMaintenanceDownloadProjectionServiceTests
{
    [Fact]
    public void ToCachedDownloads_MapsCacheSizesByPath()
    {
        MediaMaintenanceDownloadProjectionService sut = new();
        List<MediaDownload> downloads =
        [
            new()
            {
                Id = "d1",
                Data =
                [
                    new MediaDownloadData { Url = "u1", Path = "p1" },
                    new MediaDownloadData { Url = "u2", Path = "p2" },
                ],
            },
        ];
        Dictionary<string, long?> cacheSizes = new() { ["p1"] = 123 };

        IReadOnlyList<MediaMaintenanceCachedDownload> result = sut.ToCachedDownloads(
            downloads,
            cacheSizes
        );

        Assert.Single(result);
        Assert.Equal("d1", result[0].Id);
        Assert.Equal(2, result[0].Data.Count);
        Assert.Equal(123, result[0].Data[0].CacheFileSize);
        Assert.Null(result[0].Data[1].CacheFileSize);
    }

    [Fact]
    public void ToDownloads_MapsBackToMediaDownloads()
    {
        MediaMaintenanceDownloadProjectionService sut = new();
        List<MediaMaintenanceCachedDownload> cached =
        [
            new()
            {
                Id = "d1",
                Data =
                [
                    new MediaMaintenanceCachedDownloadData
                    {
                        Url = "u1",
                        Path = "p1",
                        CacheFileSize = 999,
                    },
                ],
            },
        ];

        IReadOnlyList<MediaDownload> result = sut.ToDownloads(cached);

        Assert.Single(result);
        Assert.Equal("d1", result[0].Id);
        Assert.Single(result[0].Data);
        Assert.Equal("u1", result[0].Data[0].Url);
        Assert.Equal("p1", result[0].Data[0].Path);
    }
}
