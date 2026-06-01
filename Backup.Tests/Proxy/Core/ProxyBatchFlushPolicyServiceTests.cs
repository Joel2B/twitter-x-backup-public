using Backup.Application.Proxy;

namespace Backup.Tests;

public class ProxyBatchFlushPolicyServiceTests
{
    [Theory]
    [InlineData(0, 10, false)]
    [InlineData(1, 10, false)]
    [InlineData(10, 10, true)]
    [InlineData(20, 10, true)]
    [InlineData(5, 0, false)]
    public void ShouldFlush_ReturnsExpected(int acceptedCount, int flushEvery, bool expected)
    {
        ProxyBatchFlushPolicyService sut = new();

        bool result = sut.ShouldFlush(acceptedCount, flushEvery);

        Assert.Equal(expected, result);
    }
}
