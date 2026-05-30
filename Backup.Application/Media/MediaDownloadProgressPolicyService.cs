namespace Backup.Application.Media;

public sealed class MediaDownloadProgressPolicyService : IMediaDownloadProgressPolicyService
{
    public int CalculatePercent(long downloadedBytes, long totalBytes) =>
        (int)(downloadedBytes * 100 / totalBytes);

    public bool ShouldEmitProgressLog(int percent, int nextPercentThreshold) =>
        percent >= nextPercentThreshold;

    public int GetNextThreshold(int currentThreshold, int stepPercent) => currentThreshold + stepPercent;
}
