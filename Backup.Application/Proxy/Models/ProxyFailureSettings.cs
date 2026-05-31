namespace Backup.Application.Proxy.Models;

public sealed class ProxyFailureSettings
{
    public int DownloadThreadStart { get; init; }
    public int AttemptsPerProxy { get; init; }
    public int ErrorsToStop { get; init; }
}
