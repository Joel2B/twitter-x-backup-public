using Backup.Application.Media;
using Backup.Application.Media.Models;

namespace Backup.Tests;

public class MediaDuplicateFilterServiceTests
{
    private readonly MediaDuplicateFilterService _sut = new();

    [Fact]
    public void Filter_RemovesDuplicatesGlobally_CaseInsensitive()
    {
        List<MediaDownload> downloads =
        [
            new()
            {
                Id = "a",
                Data =
                [
                    new() { Url = "https://x.com/1.jpg", Path = "a/1.jpg" },
                    new() { Url = "https://x.com/2.jpg", Path = "a/2.jpg" },
                ],
            },
            new()
            {
                Id = "b",
                Data =
                [
                    new() { Url = "https://x.com/2.jpg", Path = "b/2.jpg" },
                    new() { Url = "https://x.com/1.JPG", Path = "b/1.jpg" },
                    new() { Url = "https://x.com/3.jpg", Path = "b/3.jpg" },
                ],
            },
        ];

        IReadOnlyList<MediaDownload> filtered = _sut.Filter(downloads);

        Assert.Equal(2, filtered.Count);
        Assert.Equal(2, filtered[0].Data.Count);
        Assert.Single(filtered[1].Data);
        Assert.Equal("https://x.com/3.jpg", filtered[1].Data[0].Url);
    }

    [Fact]
    public void Filter_RemovesEntriesWithNoRemainingData()
    {
        List<MediaDownload> downloads =
        [
            new() { Id = "a", Data = [new() { Url = "https://x.com/1.jpg", Path = "a/1.jpg" }] },
            new() { Id = "b", Data = [new() { Url = "https://x.com/1.jpg", Path = "b/1.jpg" }] },
        ];

        IReadOnlyList<MediaDownload> filtered = _sut.Filter(downloads);

        Assert.Single(filtered);
        Assert.Equal("a", filtered[0].Id);
    }
}
