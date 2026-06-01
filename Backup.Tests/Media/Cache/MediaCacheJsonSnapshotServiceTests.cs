using Backup.Application.Media.Maintenance;
using Backup.Application.Media.Maintenance.Models;

namespace Backup.Tests;

public sealed class MediaCacheJsonSnapshotServiceTests
{
    [Fact]
    public void ParseNullableLong_ParsesKnownInputTypes()
    {
        MediaCacheJsonSnapshotService sut = new();

        Assert.Null(sut.ParseNullableLong(null));
        Assert.Equal(5L, sut.ParseNullableLong(5L));
        Assert.Equal(6L, sut.ParseNullableLong(6));
        Assert.Equal(7L, sut.ParseNullableLong("7"));
        Assert.Null(sut.ParseNullableLong("not-a-number"));
    }

    [Fact]
    public void ParseNullableInt_ParsesKnownInputTypes()
    {
        MediaCacheJsonSnapshotService sut = new();

        Assert.Null(sut.ParseNullableInt(null));
        Assert.Equal(5, sut.ParseNullableInt(5));
        Assert.Equal(6, sut.ParseNullableInt(6L));
        Assert.Equal(7, sut.ParseNullableInt("7"));
        Assert.Null(sut.ParseNullableInt("not-a-number"));
    }

    [Fact]
    public void CreateSnapshot_ReturnsNull_WhenPathIsEmpty()
    {
        MediaCacheJsonSnapshotService sut = new();

        MediaCacheJsonSnapshot? snapshot = sut.CreateSnapshot(" ", 1, 2, 3);

        Assert.Null(snapshot);
    }

    [Fact]
    public void CreateSnapshot_TrimsPathAndKeepsValues()
    {
        MediaCacheJsonSnapshotService sut = new();

        MediaCacheJsonSnapshot? snapshot = sut.CreateSnapshot(" /a/b ", 10, 20, 2);

        Assert.NotNull(snapshot);
        Assert.Equal("/a/b", snapshot.Path);
        Assert.Equal(10, snapshot.StreamSizeBytes);
        Assert.Equal(20, snapshot.FileSizeBytes);
        Assert.Equal(2, snapshot.PartitionId);
    }

    [Fact]
    public void PrepareForWrite_DeduplicatesSortsAndKeepsLast()
    {
        MediaCacheJsonSnapshotService sut = new();
        List<MediaCacheJsonSnapshot> input =
        [
            new() { Path = "B", StreamSizeBytes = 1 },
            new() { Path = "a", StreamSizeBytes = 2 },
            new() { Path = "A", StreamSizeBytes = 3 },
            new() { Path = "", StreamSizeBytes = 4 },
        ];

        IReadOnlyList<MediaCacheJsonSnapshot> result = sut.PrepareForWrite(input);

        Assert.Equal(2, result.Count);
        Assert.Equal("A", result[0].Path);
        Assert.Equal(3, result[0].StreamSizeBytes);
        Assert.Equal("B", result[1].Path);
    }
}
