namespace Backup.Application.Media;

public interface IMediaDownloadPolicyService
{
    void EnsureAllowedContentLength(
        long? contentLength,
        long maxBytes,
        Func<long, string> formatBytes
    );
    bool ShouldUseMemoryStream(long? contentLength, long inMemoryThreshold);
    bool ShouldReportProgress(long? contentLength, long progressThreshold);
    bool ShouldLogTiming(int startThreads);
}
