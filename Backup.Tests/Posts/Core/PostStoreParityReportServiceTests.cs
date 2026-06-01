using Backup.Application.Posts;
using Backup.Domain.Posts;

namespace Backup.Tests;

public sealed class PostStoreParityReportServiceTests
{
    private readonly PostStoreParityReportService _sut = new();

    [Fact]
    public void Build_WhenNoMismatches_EmitsOkStatusesForSecondaries()
    {
        PostStoreParityResult parity = new()
        {
            PrimaryLabel = "primary",
            Snapshots =
            [
                new PostStoreSnapshot
                {
                    Label = "primary",
                    Counts = new PostStoreCounts { Posts = 10, Profiles = 2 },
                },
                new PostStoreSnapshot
                {
                    Label = "secondary",
                    Counts = new PostStoreCounts { Posts = 10, Profiles = 2 },
                },
            ],
        };

        var report = _sut.Build(parity);

        Assert.Equal(2, report.Snapshots.Count);
        Assert.Single(report.Statuses);
        Assert.False(report.Statuses[0].IsMismatch);
        Assert.Equal("primary", report.Statuses[0].PrimaryLabel);
        Assert.Equal("secondary", report.Statuses[0].SecondaryLabel);
        Assert.Equal(string.Empty, report.Statuses[0].DiffsText);
    }

    [Fact]
    public void Build_WhenMismatches_EmitsMismatchStatuses()
    {
        PostStoreParityResult parity = new()
        {
            PrimaryLabel = "primary",
            Snapshots =
            [
                new PostStoreSnapshot
                {
                    Label = "primary",
                    Counts = new PostStoreCounts { Posts = 10 },
                },
                new PostStoreSnapshot
                {
                    Label = "secondary",
                    Counts = new PostStoreCounts { Posts = 9 },
                },
            ],
            Mismatches =
            [
                new PostStoreMismatch
                {
                    PrimaryLabel = "primary",
                    SecondaryLabel = "secondary",
                    Diffs = ["posts:10!=9", "profiles:2!=1"],
                },
            ],
        };

        var report = _sut.Build(parity);

        Assert.Single(report.Statuses);
        Assert.True(report.Statuses[0].IsMismatch);
        Assert.Equal("primary", report.Statuses[0].PrimaryLabel);
        Assert.Equal("secondary", report.Statuses[0].SecondaryLabel);
        Assert.Equal("posts:10!=9, profiles:2!=1", report.Statuses[0].DiffsText);
    }
}
