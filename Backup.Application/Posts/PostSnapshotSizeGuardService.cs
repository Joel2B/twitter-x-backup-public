namespace Backup.Application.Posts;

public sealed class PostSnapshotSizeGuardService : IPostSnapshotSizeGuardService
{
    public void EnsureNotShrunkBeyondThreshold(
        long currentLength,
        long historyLength,
        long threshold,
        string fileName,
        string historyDirectoryName
    )
    {
        long diff = historyLength - currentLength;
        long normalizedThreshold = Math.Max(0, threshold);

        if (diff <= normalizedThreshold)
            return;

        throw new Exception(
            $"current '{fileName}' is smaller than latest history beyond threshold: current={currentLength}, history={historyLength}, shrink={diff}, threshold={normalizedThreshold}, historyDir='{historyDirectoryName}'"
        );
    }
}
