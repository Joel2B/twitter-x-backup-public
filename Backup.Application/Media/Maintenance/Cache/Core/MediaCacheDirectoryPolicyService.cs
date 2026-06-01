namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheDirectoryPolicyService : IMediaCacheDirectoryPolicyService
{
    public bool ShouldCreateCacheDirectory(string partitionType, IReadOnlyList<string>? tags)
    {
        string normalizedType = partitionType.ToLowerInvariant();

        if (normalizedType is "primary" or "cache")
            return true;

        return tags is not null && tags.Contains("cache");
    }

    public bool ShouldCreateMediaDirectory(string partitionType)
    {
        string normalizedType = partitionType.ToLowerInvariant();
        return normalizedType is "primary" or "extension" or "heavy";
    }
}
