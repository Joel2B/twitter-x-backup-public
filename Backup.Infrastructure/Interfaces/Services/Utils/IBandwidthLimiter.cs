namespace Backup.Infrastructure.Utility.Abstractions.Services;

public interface IBandwidthLimiter
{
    public Task Throttle(int read, CancellationToken token);
}
