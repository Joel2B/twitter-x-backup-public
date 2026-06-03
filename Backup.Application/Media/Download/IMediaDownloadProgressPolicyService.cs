namespace Backup.Application.Media;

public interface IMediaDownloadProgressPolicyService
{
    int CalculatePercent(long downloadedBytes, long totalBytes);
    bool ShouldEmitProgressLog(int percent, int nextPercentThreshold);
    int GetNextThreshold(int currentThreshold, int stepPercent);
}
