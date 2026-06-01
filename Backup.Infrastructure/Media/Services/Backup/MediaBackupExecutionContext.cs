using System.Collections.Concurrent;
using Backup.Application.Media.Backup.Models;
using Backup.Infrastructure.Media.Abstractions.Services;
using Backup.Infrastructure.Media.Models.Backup;

namespace Backup.Infrastructure.Media.Services;

internal sealed class MediaBackupExecutionContext(BackupChunks initialBackup)
{
    public List<string> Paths { get; set; } = [];
    public IMediaStorage? MediaData { get; set; }
    public BackupChunks Backup { get; set; } = initialBackup;
    public Dictionary<int, Chunk> Chunks { get; set; } = [];
    public List<string> PathsInBoth { get; set; } = [];
    public ConcurrentBag<string> PathsDirect { get; set; } = [];
    public List<MediaBackupIntegrityChange> Changes { get; } = [];
}
