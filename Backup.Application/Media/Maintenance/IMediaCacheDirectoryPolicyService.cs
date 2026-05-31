namespace Backup.Application.Media.Maintenance;

public interface IMediaCacheDirectoryPolicyService
{
    bool ShouldCreateCacheDirectory(string partitionType, IReadOnlyList<string>? tags);
    bool ShouldCreateMediaDirectory(string partitionType);
}
