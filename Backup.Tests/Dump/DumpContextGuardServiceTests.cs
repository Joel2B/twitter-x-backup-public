using Backup.Application.Dump;

namespace Backup.Tests;

public class DumpContextGuardServiceTests
{
    private readonly DumpContextGuardService _sut = new();

    [Fact]
    public void ResolveType_ReturnsContextId_WhenNoCurrentSession()
    {
        string type = _sut.ResolveType(null, "SearchTimeline", null);

        Assert.Equal("SearchTimeline", type);
    }

    [Fact]
    public void ResolveType_ReturnsContextId_WhenCurrentSessionAndSameType()
    {
        string type = _sut.ResolveType("2026.05.30-10.00.00", "SearchTimeline", "SearchTimeline");

        Assert.Equal("SearchTimeline", type);
    }

    [Fact]
    public void ResolveType_Throws_WhenCurrentSessionAndDifferentType()
    {
        Assert.Throws<Exception>(
            () => _sut.ResolveType("2026.05.30-10.00.00", "SearchTimeline", "TweetDetail")
        );
    }
}
