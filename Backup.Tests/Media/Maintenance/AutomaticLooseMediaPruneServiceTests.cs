using Backup.Infrastructure.Media.Maintenance;

namespace Backup.Tests.Media.Maintenance;

public sealed class AutomaticLooseMediaPruneServiceTests
{
    [Theory]
    [InlineData("1/photo/2/source/jpg/size/orig.jpg", "1/photo/2/source")]
    [InlineData("1/video/2/thumb/source/jpg/size/orig.jpg", "1/video/2/thumb/source")]
    [InlineData("1/gif/2/thumb/source/png/size/orig.png", "1/gif/2/thumb/source")]
    [InlineData("1/video/2/mp4/source/1920x1080.mp4", "1/video/2")]
    public void Group_keeps_media_boundaries(string path, string expected)
    {
        Assert.Equal(expected, AutomaticLooseMediaPruneService.Group(path));
    }

    [Fact]
    public void Normalize_accepts_cache_separator_variants()
    {
        Assert.Equal(
            "1/photo/2/source/jpg/size/orig.jpg",
            AutomaticLooseMediaPruneService.Normalize(
                "1\\photo\\2\\source\\jpg\\size\\orig.jpg"
            )
        );
    }

    [Fact]
    public void Approval_hash_is_deterministic_and_covers_operations()
    {
        Guid runId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        AutomaticLooseMediaPruneService.PruneOperation operation = new(
            runId,
            0,
            "1/photo/2/source/jpg/size/large.jpg",
            7,
            123,
            456,
            "1/photo/2/source/jpg/size/orig.jpg",
            7,
            789,
            101112,
            "pending"
        );

        string first = AutomaticLooseMediaPruneService.CalculateApprovalHash(
            runId,
            "nas media",
            "postgres",
            "namespace",
            new('a', 64),
            new('b', 64),
            [operation]
        );
        string second = AutomaticLooseMediaPruneService.CalculateApprovalHash(
            runId,
            "nas media",
            "postgres",
            "namespace",
            new('a', 64),
            new('b', 64),
            [operation with { FileSize = 124 }]
        );

        Assert.Equal(64, first.Length);
        Assert.NotEqual(first, second);
        Assert.True(AutomaticLooseMediaPruneService.FixedEquals(first, first));
        Assert.False(AutomaticLooseMediaPruneService.FixedEquals(first, second));
        Assert.False(AutomaticLooseMediaPruneService.FixedEquals(first, "invalid"));
    }
}
