using Backup.Application.Bulk;
using Backup.Application.Bulk.Models;

namespace Backup.Tests;

public class BulkSourceRouteServiceTests
{
    private readonly BulkSourceRouteService _sut = new();

    [Fact]
    public void GetOrigin_ReturnsExpectedValues()
    {
        Assert.Equal("media", _sut.GetOrigin(BulkSourceType.Media));
        Assert.Equal("notifications", _sut.GetOrigin(BulkSourceType.Notifications));
        Assert.Null(_sut.GetOrigin(BulkSourceType.Status));
        Assert.Null(_sut.GetOrigin(BulkSourceType.None));
    }

    [Fact]
    public void GetReferer_WithoutUserName_UsesOriginPath()
    {
        string referer = _sut.GetReferer(BulkSourceType.Notifications);

        Assert.Equal("https://x.com/notifications", referer);
    }

    [Fact]
    public void GetReferer_WithUserName_UsesUserOriginPath()
    {
        string referer = _sut.GetReferer(BulkSourceType.Media, "alice");

        Assert.Equal("https://x.com/alice/media", referer);
    }

    [Fact]
    public void GetReferer_WhenOriginUnknown_ReturnsRootUrl()
    {
        string referer = _sut.GetReferer(BulkSourceType.None);

        Assert.Equal("https://x.com/", referer);
    }
}
