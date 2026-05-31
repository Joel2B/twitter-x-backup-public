using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Abstractions.Services;

public interface IMediaBackupChunkPersistenceIOService
{
    Task SaveChunk(IMediaBackupData backupData, Chunk chunk);
}
