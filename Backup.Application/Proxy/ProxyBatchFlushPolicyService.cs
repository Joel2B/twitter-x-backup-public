namespace Backup.Application.Proxy;

public sealed class ProxyBatchFlushPolicyService : IProxyBatchFlushPolicyService
{
    public bool ShouldFlush(int acceptedCount, int flushEvery)
    {
        if (acceptedCount <= 0 || flushEvery <= 0)
            return false;

        return acceptedCount % flushEvery == 0;
    }
}
