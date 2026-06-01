using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyConnectionWindowPolicyServiceTests
{
    private readonly IProxyConnectionWindowPolicyService _sut =
        new ProxyConnectionWindowPolicyService();

    [Fact]
    public void GetWindowKey_FormatsAtHourPrecision()
    {
        DateTime value = new(2026, 5, 30, 14, 45, 59);
        string key = _sut.GetWindowKey(value);
        Assert.Equal("2026-05-30, 14", key);
    }

    [Fact]
    public void IsSameWindow_ReturnsTrue_ForSameHour()
    {
        DateTime left = new(2026, 5, 30, 14, 10, 0);
        DateTime right = new(2026, 5, 30, 14, 59, 59);
        Assert.True(_sut.IsSameWindow(left, right));
    }

    [Fact]
    public void IsSameWindow_ReturnsFalse_ForDifferentHour()
    {
        DateTime left = new(2026, 5, 30, 14, 59, 59);
        DateTime right = new(2026, 5, 30, 15, 00, 00);
        Assert.False(_sut.IsSameWindow(left, right));
    }
}
