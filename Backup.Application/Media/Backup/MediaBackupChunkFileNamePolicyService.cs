namespace Backup.Application.Media.Backup;

public sealed class MediaBackupChunkFileNamePolicyService
    : IMediaBackupChunkFileNamePolicyService
{
    public string BuildDataFileName(int chunkId, string extension) => $"{chunkId}.{extension}";

    public string BuildZipFileName(int chunkId, string extension) => $"{chunkId}.{extension}";
}
