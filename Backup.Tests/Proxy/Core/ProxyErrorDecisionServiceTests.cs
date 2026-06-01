using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyErrorDecisionServiceTests
{
    [Fact]
    public void Decide_ReturnsNewAndDisable_WhenThresholdReached()
    {
        IProxyRuntimePolicyService runtimePolicy = new ProxyRuntimePolicyService();
        ProxyErrorDecisionService sut = new(runtimePolicy);
        IReadOnlyCollection<string> existing = ["timeout", "forbidden"];

        var decision = sut.Decide(existing, "ssl", 3);

        Assert.True(decision.IsNewMessage);
        Assert.True(decision.ShouldDisable);
    }

    [Fact]
    public void Decide_ReturnsDuplicateAndNoDisable_WhenBelowThreshold()
    {
        IProxyRuntimePolicyService runtimePolicy = new ProxyRuntimePolicyService();
        ProxyErrorDecisionService sut = new(runtimePolicy);
        IReadOnlyCollection<string> existing = ["timeout"];

        var decision = sut.Decide(existing, "timeout", 5);

        Assert.False(decision.IsNewMessage);
        Assert.False(decision.ShouldDisable);
    }
}
