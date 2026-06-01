using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyRuntimePolicyServiceTests
{
    private readonly IProxyRuntimePolicyService _sut = new ProxyRuntimePolicyService();

    [Fact]
    public void ShouldIncludeInRuntimePool_ReturnsTrue_WhenActive()
    {
        Assert.True(_sut.ShouldIncludeInRuntimePool(true, 0));
    }

    [Fact]
    public void ShouldIncludeInRuntimePool_ReturnsTrue_WhenHasConnections()
    {
        Assert.True(_sut.ShouldIncludeInRuntimePool(false, 1));
    }

    [Fact]
    public void ShouldAttemptProxySwitch_ReturnsTrue_WhenFailureCountReachesThreshold()
    {
        Assert.True(_sut.ShouldAttemptProxySwitch(3, 3));
        Assert.False(_sut.ShouldAttemptProxySwitch(2, 3));
    }

    [Fact]
    public void ShouldRotateProxy_ReturnsTrue_WhenAttemptsReached()
    {
        Assert.True(_sut.ShouldRotateProxy(3, 3));
        Assert.False(_sut.ShouldRotateProxy(2, 3));
    }

    [Fact]
    public void ShouldDisableProxy_ReturnsTrue_WhenErrorsReachThreshold()
    {
        Assert.True(_sut.ShouldDisableProxy(5, 5));
        Assert.False(_sut.ShouldDisableProxy(4, 5));
    }

    [Fact]
    public void StopThresholdRules_WorkAsExpected()
    {
        Assert.True(_sut.IsStopThresholdDisabled(-1));
        Assert.False(_sut.IsStopThresholdDisabled(3));
        Assert.True(_sut.ShouldStopProcess(3, 3));
        Assert.False(_sut.ShouldStopProcess(2, 3));
    }
}
