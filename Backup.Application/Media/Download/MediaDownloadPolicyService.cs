namespace Backup.Application.Media;

public sealed class MediaDownloadPolicyService : IMediaDownloadPolicyService
{
    public void EnsureAllowedContentLength(
        long? contentLength,
        long maxBytes,
        Func<long, string> formatBytes
    )
    {
        if (contentLength is null || maxBytes <= 0)
            return;

        if (contentLength.Value < maxBytes)
            return;

        throw new SystemException($">= {formatBytes(maxBytes)}");
    }

    public bool ShouldUseMemoryStream(long? contentLength, long inMemoryThreshold) =>
        contentLength is not null && contentLength.Value <= inMemoryThreshold;

    public bool ShouldReportProgress(long? contentLength, long progressThreshold) =>
        contentLength is not null && contentLength.Value >= progressThreshold;

    public bool ShouldLogTiming(int startThreads) => startThreads == 1;
}
