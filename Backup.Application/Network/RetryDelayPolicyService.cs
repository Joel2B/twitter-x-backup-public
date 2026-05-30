namespace Backup.Application.Network;

public sealed class RetryDelayPolicyService : IRetryDelayPolicyService
{
    public int GetDelayMilliseconds(int minSeconds, int maxSeconds)
    {
        int min = Math.Max(1, minSeconds);
        int max = Math.Max(min, maxSeconds);

        return Random.Shared.Next(min * 1000, max * 1000 + 1);
    }
}
