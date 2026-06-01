namespace Backup.Application.Proxy;

public interface IProxyBatchFlushPolicyService
{
    bool ShouldFlush(int acceptedCount, int flushEvery);
}
