using Backup.Application.Media.Models;

namespace Backup.Application.Media;

public sealed class MediaDownloadStreamingPolicyService : IMediaDownloadStreamingPolicyService
{
    public MediaDownloadStreamingSettings GetSettings() =>
        new()
        {
            BufferSizeBytes = 128 * 1024,
            ProgressThresholdBytes = 10L * 1024 * 1024,
            ProgressStepPercent = 10,
        };

    public string BuildNoDataTimeoutMessage(int timeoutMilliseconds) =>
        $"No data received in {timeoutMilliseconds} ms.";
}
