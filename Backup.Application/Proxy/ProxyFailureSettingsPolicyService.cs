using Backup.Application.Proxy.Models;

namespace Backup.Application.Proxy;

public sealed class ProxyFailureSettingsPolicyService : IProxyFailureSettingsPolicyService
{
    private const int DefaultAttemptsPerProxy = 3;

    public ProxyFailureSettings Create(int downloadThreadStart, int errorsToStop) =>
        new()
        {
            DownloadThreadStart = downloadThreadStart,
            AttemptsPerProxy = DefaultAttemptsPerProxy,
            ErrorsToStop = errorsToStop,
        };
}
