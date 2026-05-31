using Backup.Application.Proxy;

namespace Backup.Tests;

public sealed class ProxyRuntimeStatusTransitionServiceTests
{
    [Fact]
    public void ShouldHandleError_Returns_False_For_Inactive_Proxy()
    {
        ProxyRuntimeStatusTransitionService sut = new();
        bool result = sut.ShouldHandleError(isCurrentlyActive: false);
        Assert.False(result);
    }

    [Fact]
    public void ResolveDisabledAt_Returns_Now_When_Disabled()
    {
        ProxyRuntimeStatusTransitionService sut = new();
        DateTime now = new(2026, 05, 30, 10, 0, 0, DateTimeKind.Utc);

        DateTime? result = sut.ResolveDisabledAt(true, now);

        Assert.Equal(now, result);
    }

    [Fact]
    public void ResolveStatus_Active_Runtime_Keeps_Previous_Date()
    {
        ProxyRuntimeStatusTransitionService sut = new();
        DateTime previous = new(2026, 05, 30, 9, 0, 0, DateTimeKind.Utc);

        var result = sut.ResolveStatus(runtimeIsActive: true, previous, disabledAt: null);

        Assert.True(result.IsActive);
        Assert.Equal(previous, result.StatusDate);
    }

    [Fact]
    public void ResolveStatus_Inactive_Runtime_Uses_DisabledAt_When_Present()
    {
        ProxyRuntimeStatusTransitionService sut = new();
        DateTime previous = new(2026, 05, 30, 9, 0, 0, DateTimeKind.Utc);
        DateTime disabledAt = new(2026, 05, 30, 11, 0, 0, DateTimeKind.Utc);

        var result = sut.ResolveStatus(runtimeIsActive: false, previous, disabledAt);

        Assert.False(result.IsActive);
        Assert.Equal(disabledAt, result.StatusDate);
    }
}
