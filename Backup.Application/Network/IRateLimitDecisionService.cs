using Backup.Application.Network.Models;

namespace Backup.Application.Network;

public interface IRateLimitDecisionService
{
    RateLimitDecision Evaluate(
        int limit,
        int remaining,
        int thresholdPercent,
        bool waitResetEnabled,
        DateTimeOffset now,
        DateTimeOffset resetAt
    );
}
