namespace Backup.Application.Posts;

public interface IPostSnapshotSizeGuardService
{
    void EnsureNotShrunkBeyondThreshold(
        long currentLength,
        long historyLength,
        long threshold,
        string fileName,
        string historyDirectoryName
    );
}
