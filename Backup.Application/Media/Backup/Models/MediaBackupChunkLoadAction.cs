namespace Backup.Application.Media.Backup.Models;

public enum MediaBackupChunkLoadAction
{
    SkipAsNull = 0,
    ReturnEmpty = 1,
    Load = 2,
}
