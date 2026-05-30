using Backup.Infrastructure.Bulk.Adapters;
using Backup.Infrastructure.Bulk.Models;

namespace Backup.Tests;

public class BulkSourceRouteProviderTests
{
    private readonly BulkSourceRouteProvider _sut = new();

    [Fact]
    public void GetOrigin_ReturnsExpectedValues()
    {
        Assert.Equal("media", _sut.GetOrigin(SourceType.Media));
        Assert.Equal("notifications", _sut.GetOrigin(SourceType.Notifications));
        Assert.Null(_sut.GetOrigin(SourceType.Status));
        Assert.Null(_sut.GetOrigin(SourceType.None));
    }

    [Fact]
    public void GetReferer_WithoutUserName_UsesOriginPath()
    {
        string referer = _sut.GetReferer(SourceType.Notifications);

        Assert.Equal("https://x.com/notifications", referer);
    }

    [Fact]
    public void GetReferer_WithUserName_UsesUserOriginPath()
    {
        string referer = _sut.GetReferer(SourceType.Media, "alice");

        Assert.Equal("https://x.com/alice/media", referer);
    }

    [Fact]
    public void GetReferer_WhenOriginUnknown_StillBuildsUrl()
    {
        string referer = _sut.GetReferer(SourceType.None);

        Assert.Equal("https://x.com/", referer);
    }
}
