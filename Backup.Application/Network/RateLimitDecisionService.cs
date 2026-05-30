using Backup.Application.Network.Models;

namespace Backup.Application.Network;

public sealed class RateLimitDecisionService : IRateLimitDecisionService
{
    public RateLimitDecision Evaluate(
        int limit,
        int remaining,
        int thresholdPercent,
        bool waitResetEnabled,
        DateTimeOffset now,
        DateTimeOffset resetAt
    )
    {
        int normalizedLimit = Math.Max(0, limit);
        int normalizedRemaining = Math.Max(0, remaining);
        int normalizedThresholdPercent = Math.Max(0, thresholdPercent);
        int threshold = normalizedThresholdPercent * normalizedLimit / 100;

        TimeSpan diff = resetAt - now;
        int waitMs = (int)Math.Max(0, diff.TotalMilliseconds);

        if (normalizedRemaining <= threshold)
        {
            if (waitResetEnabled)
            {
                return new RateLimitDecision
                {
                    Continue = true,
                    Threshold = threshold,
                    WaitMilliseconds = waitMs,
                };
            }

            return new RateLimitDecision
            {
                Continue = false,
                Threshold = threshold,
                WaitMilliseconds = 0,
            };
        }

        return new RateLimitDecision
        {
            Continue = true,
            Threshold = threshold,
            WaitMilliseconds = 0,
        };
    }
}
