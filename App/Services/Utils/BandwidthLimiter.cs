using Backup.App.Interfaces.Services.UtilsService;

namespace Backup.App.Services.UtilsService;

public class BandwidthLimiter(Models.Config.App config) : IBandwidthLimiter
{
    private readonly long _maxBytesPerSecond = Math.Max(0, config.Downloads.MaxBytesPerSecond);

    private long _bytesThisSecond = 0;
    private long _currentSecond = 0;
    private readonly object _sync = new();

    public async Task Throttle(int bytesRead, CancellationToken token)
    {
        if (_maxBytesPerSecond <= 0)
            return;

        while (true)
        {
            token.ThrowIfCancellationRequested();

            DateTimeOffset now = DateTimeOffset.UtcNow;
            long nowSec = now.ToUnixTimeSeconds();
            bool wait = false;
            int delayMs = 0;

            lock (_sync)
            {
                if (nowSec != _currentSecond)
                {
                    _currentSecond = nowSec;
                    _bytesThisSecond = 0;
                }

                if (_bytesThisSecond + bytesRead > _maxBytesPerSecond)
                {
                    wait = true;
                    delayMs = Math.Max(25, 1000 - now.Millisecond);
                }
                else
                    _bytesThisSecond += bytesRead;
            }

            if (!wait)
                break;

            await Task.Delay(delayMs, token);
        }
    }
}
