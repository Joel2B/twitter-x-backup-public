using Backup.Application.Media.Integrity;
using Backup.Application.Media.Models;

namespace Backup.Tests;

public class MediaIntegrityPolicyServiceTests
{
    [Fact]
    public void KeepSupported_RemovesUnsupportedExtensions()
    {
        MediaIntegrityPolicyService sut = new();

        List<MediaDownload> downloads =
        [
            new()
            {
                Id = "p1",
                Data =
                [
                    new() { Url = "https://x/a.jpg", Path = "a.jpg" },
                    new() { Url = "https://x/a.gif", Path = "a.gif" },
                    new() { Url = "https://x/a.mp4", Path = "a.mp4" },
                ],
            },
            new()
            {
                Id = "p2",
                Data = [new() { Url = "https://x/b.webp", Path = "b.webp" }],
            },
        ];

        sut.KeepSupported(downloads);

        Assert.Single(downloads);
        Assert.Equal("p1", downloads[0].Id);
        Assert.Equal(2, downloads[0].Data.Count);
        Assert.Contains(downloads[0].Data, item => item.Path.EndsWith(".jpg"));
        Assert.Contains(downloads[0].Data, item => item.Path.EndsWith(".mp4"));
    }
}
