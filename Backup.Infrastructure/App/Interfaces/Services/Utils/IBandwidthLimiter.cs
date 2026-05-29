namespace Backup.App.Interfaces.Services.UtilsService;

public interface IBandwidthLimiter
{
    public Task Throttle(int read, CancellationToken token);
}
