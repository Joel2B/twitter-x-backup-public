using System.Security.Cryptography;
using System.Text;
using Backup.Application.IO;

namespace Backup.Application.Media.Maintenance;

public sealed class MediaCacheEntryPathPolicyService : IMediaCacheEntryPathPolicyService
{
    public string NormalizeForCacheKey(string path) =>
        PathFormattingPolicy.NormalizePathForCurrentOs(path, save: true);

    public string NormalizeForStoragePath(string path) =>
        PathFormattingPolicy.NormalizePathForCurrentOs(path, save: false);

    public string BuildCacheSnapshotFileName(string normalizedPath, int? partitionId)
    {
        string name = $"{normalizedPath}{partitionId}";
        byte[] data = Encoding.UTF8.GetBytes(name);
        byte[] bytes = SHA256.HashData(data);
        string hash = Convert.ToHexString(bytes).ToLowerInvariant();

        return $"{hash}.cache";
    }
}
