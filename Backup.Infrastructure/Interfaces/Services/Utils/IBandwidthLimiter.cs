namespace Backup.Infrastructure.Interfaces.Services.UtilsService;

public interface IBandwidthLimiter
{
    public Task Throttle(int read, CancellationToken token);
}
