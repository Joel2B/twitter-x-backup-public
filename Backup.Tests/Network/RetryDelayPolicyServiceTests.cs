using Backup.Application.Network;

namespace Backup.Tests;

public class RetryDelayPolicyServiceTests
{
    private readonly RetryDelayPolicyService _sut = new();

    [Fact]
    public void GetDelayMilliseconds_UsesExactRange_WhenMinEqualsMax()
    {
        int ms = _sut.GetDelayMilliseconds(2, 2);

        Assert.Equal(2000, ms);
    }

    [Fact]
    public void GetDelayMilliseconds_NormalizesInvalidRange()
    {
        int ms = _sut.GetDelayMilliseconds(0, -5);

        Assert.Equal(1000, ms);
    }

    [Fact]
    public void GetDelayMilliseconds_ReturnsWithinRange()
    {
        int ms = _sut.GetDelayMilliseconds(2, 3);

        Assert.InRange(ms, 2000, 3000);
    }
}
