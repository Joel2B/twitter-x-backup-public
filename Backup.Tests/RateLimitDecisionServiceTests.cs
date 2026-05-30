using Backup.Application.Network;

namespace Backup.Tests;

public class RateLimitDecisionServiceTests
{
    private readonly RateLimitDecisionService _sut = new();

    [Fact]
    public void Evaluate_ReturnsContinueFalse_WhenRemainingBelowThreshold_AndNoWaitReset()
    {
        DateTimeOffset now = new(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset resetAt = now.AddMinutes(10);

        var decision = _sut.Evaluate(
            limit: 100,
            remaining: 9,
            thresholdPercent: 10,
            waitResetEnabled: false,
            now,
            resetAt
        );

        Assert.False(decision.Continue);
        Assert.Equal(10, decision.Threshold);
        Assert.Equal(0, decision.WaitMilliseconds);
    }

    [Fact]
    public void Evaluate_ReturnsWait_WhenRemainingBelowThreshold_AndWaitReset()
    {
        DateTimeOffset now = new(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset resetAt = now.AddSeconds(30);

        var decision = _sut.Evaluate(
            limit: 100,
            remaining: 10,
            thresholdPercent: 10,
            waitResetEnabled: true,
            now,
            resetAt
        );

        Assert.True(decision.Continue);
        Assert.Equal(10, decision.Threshold);
        Assert.True(decision.WaitMilliseconds >= 30000);
    }

    [Fact]
    public void Evaluate_ReturnsContinueWithoutWait_WhenRemainingAboveThreshold()
    {
        DateTimeOffset now = new(2026, 5, 30, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset resetAt = now.AddMinutes(1);

        var decision = _sut.Evaluate(
            limit: 100,
            remaining: 50,
            thresholdPercent: 10,
            waitResetEnabled: true,
            now,
            resetAt
        );

        Assert.True(decision.Continue);
        Assert.Equal(10, decision.Threshold);
        Assert.Equal(0, decision.WaitMilliseconds);
    }
}
