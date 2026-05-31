using Backup.Infrastructure.Media.Abstractions.Data;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.IO;

public sealed class MediaBackupChunkPersistenceIOService : IMediaBackupChunkPersistenceIOService
{
    public Task SaveChunk(IMediaBackupData backupData, Chunk chunk) => backupData.Save([chunk]);
}
