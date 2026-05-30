using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;

namespace Backup.Tests;

public class BulkSourceExtractionServiceTests
{
    private readonly BulkSourceExtractionService _sut = new();

    [Fact]
    public void Extract_ReturnsEmpty_WhenNoMatches()
    {
        IReadOnlyList<BulkSourceLinkItem> result = _sut.Extract(["hello world"]);

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_ParsesMediaAndStatus()
    {
        IReadOnlyList<BulkSourceLinkItem> result = _sut.Extract(
            ["https://x.com/alice/media", "https://x.com/bob/status/123"]
        );

        Assert.Equal(2, result.Count);
        Assert.Contains(result, item => item.UserName == "alice" && item.Type == BulkSourceType.Media);
        Assert.Contains(result, item => item.UserName == "bob" && item.Type == BulkSourceType.Status);
    }

    [Fact]
    public void Extract_DeduplicatesByUserName()
    {
        IReadOnlyList<BulkSourceLinkItem> result = _sut.Extract(
            ["https://x.com/alice/media", "https://x.com/alice/status/123"]
        );

        Assert.Single(result);
        Assert.Equal("alice", result[0].UserName);
    }
}
