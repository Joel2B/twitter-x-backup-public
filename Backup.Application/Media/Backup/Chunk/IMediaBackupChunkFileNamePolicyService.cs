namespace Backup.Application.Media.Backup;

public interface IMediaBackupChunkFileNamePolicyService
{
    string BuildDataFileName(int chunkId, string extension);
    string BuildZipFileName(int chunkId, string extension);
}
