using Backup.App.Interfaces.Services.Media;

namespace Backup.App.Services.Media;

public class MediaDownloadControl(Models.Config.App _config) : IMediaDownloadControl
{
    private readonly Models.Config.App _config = _config;

    private int _count = 1;
    public readonly CancellationTokenSource _tokenSource = new();

    public CancellationToken Token
    {
        get => _tokenSource.Token;
        set => throw new NotImplementedException();
    }

    public void Check()
    {
        if (_config.Downloads.Count == -1)
            return;

        int current = Interlocked.Increment(ref _count);

        if (current < _config.Downloads.Count)
            return;

        Cancel();
    }

    public void Cancel()
    {
        if (_tokenSource.IsCancellationRequested)
            return;

        _tokenSource.Cancel();
    }
}
